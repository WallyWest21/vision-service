"""YOLOv8 FastAPI wrapper exposing detection, segmentation, pose, and classification."""

import io
import os
import time
from typing import Optional

from fastapi import FastAPI, File, Form, Query, UploadFile
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
    confidence: float = Form(DEFAULT_CONF),
    max_detections: int = Query(100, ge=1, le=1000),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("detect")
    results = model(img, conf=max(0.0, min(1.0, confidence)))

    detections = []
    for r in results:
        for box in r.boxes:
            detections.append(
                {
                    "label": model.names[int(box.cls[0])],
                    "classId": int(box.cls[0]),
                    "confidence": float(box.conf[0]),
                    "boundingBox": {
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
            "processingTimeMs": round((time.time() - start) * 1000, 2),
            "model": MODEL_NAME,
        }
    )


@app.post("/segment")
async def segment(
    file: UploadFile = File(...),
    confidence: float = Form(DEFAULT_CONF),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("segment")
    results = model(img, conf=max(0.0, min(1.0, confidence)))

    segmentations = []
    for r in results:
        if r.masks is not None:
            for i, mask in enumerate(r.masks.xy):
                flat_mask = [coord for p in mask for coord in (float(p[0]), float(p[1]))]
                segmentations.append(
                    {
                        "label": model.names[int(r.boxes[i].cls[0])],
                        "classId": int(r.boxes[i].cls[0]),
                        "confidence": float(r.boxes[i].conf[0]),
                        "mask": flat_mask,
                        "boundingBox": {
                            "x1": float(r.boxes[i].xyxy[0][0]),
                            "y1": float(r.boxes[i].xyxy[0][1]),
                            "x2": float(r.boxes[i].xyxy[0][2]),
                            "y2": float(r.boxes[i].xyxy[0][3]),
                        },
                    }
                )

    return JSONResponse(
        {
            "segmentations": segmentations,
            "processingTimeMs": round((time.time() - start) * 1000, 2),
        }
    )


_COCO_KEYPOINT_NAMES = [
    "nose", "left_eye", "right_eye", "left_ear", "right_ear",
    "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
    "left_wrist", "right_wrist", "left_hip", "right_hip",
    "left_knee", "right_knee", "left_ankle", "right_ankle",
]


@app.post("/pose")
async def pose(
    file: UploadFile = File(...),
    confidence: float = Form(DEFAULT_CONF),
):
    start = time.time()
    img = read_image(await file.read())
    model = get_model("pose")
    results = model(img, conf=max(0.0, min(1.0, confidence)))

    poses = []
    for r in results:
        if r.keypoints is not None:
            kp_confs = r.keypoints.conf
            for i, kpts in enumerate(r.keypoints.xy):
                keypoints = []
                for j, kp in enumerate(kpts):
                    kp_conf = float(kp_confs[i][j]) if kp_confs is not None else 0.0
                    keypoints.append(
                        {
                            "name": _COCO_KEYPOINT_NAMES[j] if j < len(_COCO_KEYPOINT_NAMES) else str(j),
                            "x": float(kp[0]),
                            "y": float(kp[1]),
                            "confidence": kp_conf,
                        }
                    )
                poses.append(
                    {
                        "confidence": float(r.boxes[i].conf[0]),
                        "keypoints": keypoints,
                        "boundingBox": {
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
            "processingTimeMs": round((time.time() - start) * 1000, 2),
            "model": MODEL_NAME,
        }
    )


@app.post("/classify")
async def classify(
    file: UploadFile = File(...),
    top_n: int = Form(5),
):
    start = time.time()
    top_n = max(1, min(20, top_n))
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
                    "label": model.names[int(idx)],
                    "classId": int(idx),
                    "confidence": float(probs.data[idx]),
                }
            )

    return JSONResponse(
        {
            "classifications": classifications,
            "processingTimeMs": round((time.time() - start) * 1000, 2),
            "model": MODEL_NAME,
        }
    )
