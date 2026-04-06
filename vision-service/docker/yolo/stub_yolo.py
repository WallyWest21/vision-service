"""Minimal YOLO stub — CPU/no-GPU development only.

Returns a single hard-coded detection/segmentation/classification/pose so the
VisionService and MAUI client can be exercised without a real YOLOv8 model.
"""
import time

from fastapi import FastAPI, File, Form, UploadFile
from fastapi.responses import JSONResponse

app = FastAPI(title="YOLO CPU Stub")

_BOX = {"x1": 50.0, "y1": 40.0, "x2": 320.0, "y2": 280.0}

_COCO_NAMES = [
    "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train",
    "truck", "boat", "traffic light", "fire hydrant", "stop sign",
    "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep",
    "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
    "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard",
    "sports ball", "kite", "baseball bat", "baseball glove", "skateboard",
    "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork",
    "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
    "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair",
    "couch", "potted plant", "bed", "dining table", "toilet", "tv",
    "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave",
    "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase",
    "scissors", "teddy bear", "hair drier", "toothbrush",
]


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/detect")
async def detect(
    file: UploadFile = File(...),
    confidence: float = Form(0.5),
):
    return JSONResponse({
        "detections": [
            {
                "label": "person",
                "confidence": 0.87,
                "boundingBox": _BOX,
                "classId": 0,
            }
        ]
    })


@app.post("/segment")
async def segment(
    file: UploadFile = File(...),
    confidence: float = Form(0.5),
):
    return JSONResponse({
        "segmentations": [
            {
                "label": "person",
                "confidence": 0.84,
                "boundingBox": _BOX,
                "mask": [50.0, 40.0, 320.0, 40.0, 320.0, 280.0, 50.0, 280.0],
                "classId": 0,
            }
        ]
    })


@app.post("/classify")
async def classify(
    file: UploadFile = File(...),
    top_n: int = Form(5),
):
    results = [
        {"label": _COCO_NAMES[i], "confidence": round(0.9 - i * 0.15, 2), "classId": i}
        for i in range(min(top_n, 5))
    ]
    return JSONResponse({"classifications": results})


@app.post("/pose")
async def pose(
    file: UploadFile = File(...),
    confidence: float = Form(0.5),
):
    keypoints = [
        {"name": name, "x": float(50 + i * 20), "y": float(40 + i * 10), "confidence": 0.9}
        for i, name in enumerate([
            "nose", "left_eye", "right_eye", "left_ear", "right_ear",
            "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
            "left_wrist", "right_wrist", "left_hip", "right_hip",
            "left_knee", "right_knee", "left_ankle", "right_ankle",
        ])
    ]
    return JSONResponse({
        "poses": [
            {
                "boundingBox": _BOX,
                "confidence": 0.88,
                "keypoints": keypoints,
            }
        ]
    })


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=7860)
