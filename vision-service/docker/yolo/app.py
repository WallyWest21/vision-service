"""YOLOv8 FastAPI wrapper exposing detection, segmentation, pose, and classification."""

import io
import os
import time
from typing import Optional

from fastapi import FastAPI, File, Query, UploadFile
from fastapi.responses import JSONResponse
from PIL import Image
from ultralytics import YOLO

app = FastAPI(title="YOLO Vision API", version="1.0.0")

MODEL_NAME = os.getenv("YOLO_MODEL", "yolov8n.pt")
DEFAULT_CONF = float(os.getenv("CONFIDENCE_THRESHOLD", "0.5"))

# Load models lazily
_models: dict[str, YOLO] = {}


def get_model(task: str = "detect") -> YOLO:
    suffix_map = {"detect": "", "segment": "-seg", "pose": "-pose", "classify": "-cls"}
    key = task
    if key not in _models:
        base = MODEL_NAME.replace(".pt", "")
        model_file = f"{base}{suffix_map.get(task, '')}.pt"
        _models[key] = YOLO(model_file)
    return _models[key]


def read_image(file_bytes: bytes) -> Image.Image:
    return Image.open(io.BytesIO(file_bytes))


@app.get("/health")
async def health():
    return {"status": "healthy", "model": MODEL_NAME}


@app.post("/detect")
async def detect(
    file: UploadFile = File(...),
    confidence: float = Query(DEFAULT_CONF, ge=0.0, le=1.0),
    max_detections: int = Query(100, ge=1, le=1000),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("detect")
    results = model(img, conf=confidence)

    detections = []
    for r in results:
        for box in r.boxes:
            detections.append(
                {
                    "class_id": int(box.cls[0]),
                    "class_name": model.names[int(box.cls[0])],
                    "confidence": float(box.conf[0]),
                    "bbox": {
                        "x1": float(box.xyxy[0][0]),
                        "y1": float(box.xyxy[0][1]),
                        "x2": float(box.xyxy[0][2]),
                        "y2": float(box.xyxy[0][3]),
                    },
                }
            )

    return JSONResponse(
        {
            "detections": detections[:max_detections],
            "count": len(detections[:max_detections]),
            "image_size": {"width": img.width, "height": img.height},
            "processing_time_ms": round((time.time() - start) * 1000, 2),
            "model": MODEL_NAME,
        }
    )


@app.post("/segment")
async def segment(
    file: UploadFile = File(...),
    confidence: float = Query(DEFAULT_CONF, ge=0.0, le=1.0),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("segment")
    results = model(img, conf=confidence)

    segments = []
    for r in results:
        if r.masks is not None:
            for i, mask in enumerate(r.masks.xy):
                segments.append(
                    {
                        "class_id": int(r.boxes[i].cls[0]),
                        "class_name": model.names[int(r.boxes[i].cls[0])],
                        "confidence": float(r.boxes[i].conf[0]),
                        "polygon": [[float(p[0]), float(p[1])] for p in mask],
                        "bbox": {
                            "x1": float(r.boxes[i].xyxy[0][0]),
                            "y1": float(r.boxes[i].xyxy[0][1]),
                            "x2": float(r.boxes[i].xyxy[0][2]),
                            "y2": float(r.boxes[i].xyxy[0][3]),
                        },
                    }
                )

    return JSONResponse(
        {
            "segments": segments,
            "count": len(segments),
            "image_size": {"width": img.width, "height": img.height},
            "processing_time_ms": round((time.time() - start) * 1000, 2),
        }
    )


@app.post("/pose")
async def pose(
    file: UploadFile = File(...),
    confidence: float = Query(DEFAULT_CONF, ge=0.0, le=1.0),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("pose")
    results = model(img, conf=confidence)

    poses = []
    for r in results:
        if r.keypoints is not None:
            for i, kpts in enumerate(r.keypoints.xy):
                keypoints = []
                for j, kp in enumerate(kpts):
                    keypoints.append(
                        {"id": j, "x": float(kp[0]), "y": float(kp[1])}
                    )
                poses.append(
                    {
                        "person_id": i,
                        "confidence": float(r.boxes[i].conf[0]),
                        "keypoints": keypoints,
                        "bbox": {
                            "x1": float(r.boxes[i].xyxy[0][0]),
                            "y1": float(r.boxes[i].xyxy[0][1]),
                            "x2": float(r.boxes[i].xyxy[0][2]),
                            "y2": float(r.boxes[i].xyxy[0][3]),
                        },
                    }
                )

    return JSONResponse(
        {
            "poses": poses,
            "count": len(poses),
            "image_size": {"width": img.width, "height": img.height},
            "processing_time_ms": round((time.time() - start) * 1000, 2),
        }
    )


@app.post("/classify")
async def classify(
    file: UploadFile = File(...),
    top_n: int = Query(5, ge=1, le=20),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("classify")
    results = model(img)

    classifications = []
    for r in results:
        probs = r.probs
        top_indices = probs.top5[:top_n]
        for idx in top_indices:
            classifications.append(
                {
                    "class_id": int(idx),
                    "class_name": model.names[int(idx)],
                    "confidence": float(probs.data[idx]),
                }
            )

    return JSONResponse(
        {
            "classifications": classifications,
            "image_size": {"width": img.width, "height": img.height},
            "processing_time_ms": round((time.time() - start) * 1000, 2),
        }
    )
