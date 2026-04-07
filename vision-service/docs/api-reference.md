# API Reference

Complete reference for all VisionService endpoints. The interactive Swagger UI is available at **[http://localhost:5100/swagger](http://localhost:5100/swagger)** when the service is running.

---

## Table of Contents

1. [Authentication](#authentication)
2. [YOLO Endpoints](#yolo-endpoints)
3. [Qwen-VL Endpoints](#qwen-vl-endpoints)
4. [Pipeline Endpoints](#pipeline-endpoints)
5. [Admin Endpoints](#admin-endpoints)
6. [Playground Endpoint](#playground-endpoint)
7. [WebSocket Streaming](#websocket-streaming)
8. [Health Endpoints](#health-endpoints)
9. [Metrics Endpoint](#metrics-endpoint)
10. [Error Responses](#error-responses)

---

## Authentication

When `Auth:Enabled` is `true`, every request (except `/health*` and `/metrics`) must include the `X-Api-Key` header.

```bash
curl http://localhost:5100/api/v1/detect \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

Missing or invalid keys return `401 Unauthorized`. Insufficient scope returns `403 Forbidden`.

**Available scopes:**

| Scope | Grants access to |
|-------|-----------------|
| `detect` | YOLO endpoints (`/detect`, `/segment`, `/classify`, `/pose`) |
| `analyze` | Qwen-VL endpoints + pipeline endpoints |
| `admin` | Admin endpoints (`/api/v1/admin/*`) |
| `stream` | WebSocket endpoint (`/ws/stream`) |

---

## YOLO Endpoints

All YOLO endpoints accept `multipart/form-data` uploads. Responses are `application/json`.

### POST `/api/v1/detect`

Detect objects in an image and return bounding boxes, labels, and confidence scores.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `confidence` | float | `0.5` | Minimum confidence threshold (0.0–1.0) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/detect \
  -H "X-Api-Key: your-api-key" \
  -F "file=@/path/to/image.jpg" \
  -F "confidence=0.6"
```

**Response `200 OK`:**

```json
{
  "detections": [
    {
      "label": "person",
      "confidence": 0.92,
      "boundingBox": {
        "x": 120,
        "y": 45,
        "width": 180,
        "height": 320
      }
    },
    {
      "label": "car",
      "confidence": 0.87,
      "boundingBox": {
        "x": 400,
        "y": 200,
        "width": 240,
        "height": 150
      }
    }
  ],
  "processingTimeMs": 142,
  "model": "YOLOv8"
}
```

---

### POST `/api/v1/detect/batch`

Detect objects in multiple images in a single request.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `confidence` | float | `0.5` | Minimum confidence threshold (0.0–1.0) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/detect/batch \
  -H "X-Api-Key: your-api-key" \
  -F "files=@image1.jpg" \
  -F "files=@image2.jpg" \
  -F "confidence=0.5"
```

**Response `200 OK`:**

```json
[
  {
    "fileName": "image1.jpg",
    "detections": [
      {
        "label": "dog",
        "confidence": 0.95,
        "boundingBox": { "x": 50, "y": 30, "width": 200, "height": 180 }
      }
    ]
  },
  {
    "fileName": "image2.jpg",
    "detections": []
  }
]
```

---

### POST `/api/v1/segment`

Perform instance segmentation and return mask polygons alongside bounding boxes.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `confidence` | float | `0.5` | Minimum confidence threshold (0.0–1.0) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/segment \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

**Response `200 OK`:**

```json
{
  "segments": [
    {
      "label": "cat",
      "confidence": 0.89,
      "boundingBox": { "x": 80, "y": 60, "width": 150, "height": 130 },
      "mask": [[80,60],[230,60],[230,190],[80,190]]
    }
  ],
  "model": "YOLOv8-Seg"
}
```

---

### POST `/api/v1/classify`

Classify the main subject of an image and return top-N labels with scores.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `topN` | int | `5` | Number of top results to return (1–100) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/classify \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg" \
  -F "topN=3"
```

**Response `200 OK`:**

```json
{
  "classifications": [
    { "label": "golden retriever", "confidence": 0.91 },
    { "label": "labrador retriever", "confidence": 0.06 },
    { "label": "cocker spaniel", "confidence": 0.02 }
  ],
  "model": "YOLOv8-Cls"
}
```

---

### POST `/api/v1/pose`

Estimate human poses and return keypoint coordinates per person detected.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `confidence` | float | `0.5` | Minimum confidence threshold (0.0–1.0) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/pose \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

**Response `200 OK`:**

```json
{
  "poses": [
    {
      "confidence": 0.88,
      "boundingBox": { "x": 100, "y": 20, "width": 160, "height": 380 },
      "keypoints": [
        { "name": "nose", "x": 180, "y": 55, "confidence": 0.97 },
        { "name": "left_eye", "x": 195, "y": 48, "confidence": 0.95 },
        { "name": "right_eye", "x": 165, "y": 48, "confidence": 0.94 }
      ]
    }
  ],
  "model": "YOLOv8-Pose"
}
```

---

## Qwen-VL Endpoints

All Qwen-VL endpoints accept `multipart/form-data`. Text fields are passed as form fields alongside the image file.

### POST `/api/v1/ask`

Ask a natural-language question about an uploaded image.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |
| `question` | string | ✅ | Question to ask about the image |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/ask \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg" \
  -F "question=How many people are in this image?"
```

**Response `200 OK`:**

```json
{
  "text": "There are three people in the image. Two adults are standing near a table, and one child is seated.",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 42,
  "completionTokens": 28
}
```

---

### POST `/api/v1/caption`

Generate a descriptive caption for an image.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/caption \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

**Response `200 OK`:**

```json
{
  "text": "A busy street market with colorful fruit stalls and shoppers walking between the vendors.",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 18,
  "completionTokens": 22
}
```

---

### POST `/api/v1/ocr`

Extract all text visible in an image.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/ocr \
  -H "X-Api-Key: your-api-key" \
  -F "file=@sign.jpg"
```

**Response `200 OK`:**

```json
{
  "text": "OPEN\nMon–Fri 9am–6pm\nSat 10am–4pm\nClosed Sunday",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 20,
  "completionTokens": 15
}
```

---

### POST `/api/v1/analyze`

Analyze an image using a custom system prompt for structured or domain-specific output.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |
| `systemPrompt` | string | ✅ | Custom system prompt to guide the analysis |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/analyze \
  -H "X-Api-Key: your-api-key" \
  -F "file=@shelf.jpg" \
  -F "systemPrompt=You are a retail shelf analyst. List all products visible, their approximate quantities, and note any gaps in the display."
```

**Response `200 OK`:**

```json
{
  "text": "Products detected:\n- Coca-Cola 330ml cans: ~12 units (row 1)\n- Pepsi 330ml cans: ~8 units (row 2)\n- Gap identified in row 2, rightmost section\n- Sprite 500ml bottles: ~6 units (row 3)",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 55,
  "completionTokens": 60
}
```

---

### POST `/api/v1/compare`

Compare two images and describe the differences and similarities.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file1` | file | ✅ | First image |
| `file2` | file | ✅ | Second image |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/compare \
  -H "X-Api-Key: your-api-key" \
  -F "file1=@before.jpg" \
  -F "file2=@after.jpg"
```

**Response `200 OK`:**

```json
{
  "text": "The two images show the same room before and after renovation. Key differences: (1) the walls have been repainted from beige to white, (2) new wooden flooring has replaced the carpet, (3) the furniture layout has changed with the sofa now facing the window.",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 30,
  "completionTokens": 70
}
```

---

### POST `/api/v1/describe/detailed`

Generate a comprehensive, long-form scene description covering objects, spatial relationships, colors, lighting, mood, and visible text.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/describe/detailed \
  -H "X-Api-Key: your-api-key" \
  -F "file=@scene.jpg"
```

**Response `200 OK`:**

```json
{
  "text": "The image depicts a sunlit kitchen in a modern apartment. Spatial layout: a central island with bar stools occupies the foreground, while floor-to-ceiling cabinets line the back wall...",
  "model": "Qwen/Qwen2.5-VL-7B-Instruct",
  "promptTokens": 38,
  "completionTokens": 210
}
```

---

## Pipeline Endpoints

Pipeline endpoints orchestrate calls to both YOLO and Qwen-VL backends in parallel where possible.

### POST `/api/v1/pipeline/detect-and-describe`

Run YOLO object detection, then use Qwen-VL to generate a caption of the full scene.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/pipeline/detect-and-describe \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

**Response `200 OK`:**

```json
{
  "detections": [
    { "label": "bicycle", "confidence": 0.91, "boundingBox": { "x": 50, "y": 100, "width": 200, "height": 180 } },
    { "label": "person", "confidence": 0.88, "boundingBox": { "x": 200, "y": 40, "width": 120, "height": 300 } }
  ],
  "caption": "A cyclist preparing to ride their bicycle on a sunny park path.",
  "objectCount": 2
}
```

---

### POST `/api/v1/pipeline/safety-check`

Detect objects with YOLO and simultaneously ask Qwen-VL to assess whether the image contains unsafe content.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/pipeline/safety-check \
  -H "X-Api-Key: your-api-key" \
  -F "file=@image.jpg"
```

**Response `200 OK`:**

```json
{
  "isSafe": true,
  "safetyAnalysis": "SAFE — The image shows a family picnic in a public park. No unsafe, dangerous, or inappropriate content is present.",
  "detections": [
    { "label": "person", "confidence": 0.94, "boundingBox": { "x": 80, "y": 30, "width": 160, "height": 310 } }
  ]
}
```

---

### POST `/api/v1/pipeline/inventory`

Detect items with YOLO and use Qwen-VL to produce a structured inventory list.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/pipeline/inventory \
  -H "X-Api-Key: your-api-key" \
  -F "file=@warehouse.jpg"
```

**Response `200 OK`:**

```json
{
  "itemCounts": [
    { "item": "box", "count": 12 },
    { "item": "pallet", "count": 3 }
  ],
  "vlInventory": "box: 12\npallet: 3\nforklift: 1",
  "totalDetections": 16
}
```

---

### POST `/api/v1/pipeline/scene`

Full scene analysis: object detection, image captioning, and OCR all in a single request.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/pipeline/scene \
  -H "X-Api-Key: your-api-key" \
  -F "file=@storefront.jpg"
```

**Response `200 OK`:**

```json
{
  "detections": [
    { "label": "person", "confidence": 0.91, "boundingBox": { "x": 10, "y": 20, "width": 100, "height": 250 } },
    { "label": "sign", "confidence": 0.85, "boundingBox": { "x": 300, "y": 10, "width": 200, "height": 80 } }
  ],
  "caption": "A person standing outside a coffee shop with a large sign above the entrance.",
  "extractedText": "BREW & CO\nEspresso Bar\nOpen 7am–9pm",
  "detectionCount": 2
}
```

---

## Admin Endpoints

Admin endpoints require the `admin` scope.

### GET `/api/v1/admin/keys`

List all configured API keys. Key values are masked — only the last 4 characters are shown.

**Request:**

```bash
curl http://localhost:5100/api/v1/admin/keys \
  -H "X-Api-Key: your-admin-key"
```

**Response `200 OK`:**

```json
[
  {
    "name": "default",
    "scopes": ["detect", "analyze", "admin", "stream"],
    "requestsPerMinute": 0,
    "keyPreview": "...a3f9"
  }
]
```

---

### POST `/api/v1/admin/keys`

Generate and register a new API key.

**Request body** (`application/json`):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | ✅ | Display name for the key |
| `scopes` | string[] | ❌ | Scopes to grant (defaults to none) |
| `requestsPerMinute` | int | ❌ | Per-key rate limit (0 = use default) |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/admin/keys \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-admin-key" \
  -d '{"name": "my-app", "scopes": ["detect", "analyze"], "requestsPerMinute": 120}'
```

**Response `200 OK`:**

```json
{
  "key": "4a7b9c2d1e3f5g6h7i8j9k0l1m2n3o4p",
  "name": "my-app",
  "scopes": ["detect", "analyze"]
}
```

> **Important:** The full key value is only returned once at creation time. Store it securely.

---

### GET `/api/v1/admin/settings`

Retrieve all current runtime settings.

**Request:**

```bash
curl http://localhost:5100/api/v1/admin/settings \
  -H "X-Api-Key: your-admin-key"
```

**Response `200 OK`:**

```json
{
  "rateLimit": { "requestsPerMinute": 3000, "burstSize": 100 },
  "cache": { "enabled": true, "defaultTtlSeconds": 300, "maxItems": 1000 },
  "performance": {
    "minAiIntervalMs": 500,
    "maxWebSocketFrameBytes": 5242880,
    "healthCheckIntervalSeconds": 30,
    "imageCleanupIntervalHours": 6,
    "maxConcurrentAiRequests": 0
  },
  "yolo": { "baseUrl": "http://yolo-api:7860", "timeoutSeconds": 30, "maxRetries": 3 },
  "qwenVl": {
    "baseUrl": "http://qwen-vl:8000",
    "modelName": "Qwen/Qwen2.5-VL-7B-Instruct",
    "maxTokens": 1024,
    "temperature": 0.7,
    "timeoutSeconds": 120
  },
  "storage": { "retentionDays": 7, "maxFileSizeMb": 20 }
}
```

---

### PUT `/api/v1/admin/settings`

Update one or more runtime settings. Only the fields you include are updated; omitted fields are unchanged.

**Request:**

```bash
curl -X PUT http://localhost:5100/api/v1/admin/settings \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-admin-key" \
  -d '{
    "rateLimit": { "requestsPerMinute": 1200 },
    "cache": { "defaultTtlSeconds": 600 }
  }'
```

**Response `200 OK`:**

```json
{
  "message": "Settings updated. Changes take effect immediately for most settings."
}
```

---

## Playground Endpoint

### POST `/api/v1/playground`

An all-in-one endpoint that runs detection and captioning simultaneously. Designed for browser-based or quick testing.

**Form fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | Image file |

**Request:**

```bash
curl -X POST http://localhost:5100/api/v1/playground \
  -H "X-Api-Key: your-api-key" \
  -F "file=@test.jpg"
```

**Response `200 OK`:**

```json
{
  "detections": [
    { "label": "cat", "confidence": 0.96, "boundingBox": { "x": 30, "y": 40, "width": 180, "height": 160 } }
  ],
  "caption": "A fluffy orange cat sitting on a windowsill, looking outside.",
  "detectionCount": 1
}
```

---

## WebSocket Streaming

### `GET /ws/stream` (WebSocket upgrade)

Real-time frame processing. The client sends raw image frames as binary WebSocket messages and receives JSON results for each frame.

**Query parameters:**

| Parameter | Values | Default | Description |
|-----------|--------|---------|-------------|
| `mode` | `detect`, `caption` | `detect` | Processing mode per frame |

**Connection example with `websocat`:**

```bash
# Install websocat: https://github.com/vi/websocat

# Connect in detect mode
websocat -b ws://localhost:5100/ws/stream?mode=detect

# Connect in caption mode
websocat -b ws://localhost:5100/ws/stream?mode=caption
```

Once connected, send raw JPEG/PNG bytes as a binary frame. The server responds with a JSON text frame:

**Detect mode response:**

```json
{
  "mode": "detect",
  "detections": [
    { "label": "person", "confidence": 0.91, "boundingBox": { "x": 100, "y": 20, "width": 160, "height": 300 } }
  ]
}
```

**Caption mode response:**

```json
{
  "mode": "caption",
  "result": "A person walking through a hallway."
}
```

**Error response** (if the AI backend is unavailable):

```json
{
  "error": "YOLO backend unavailable: Connection refused"
}
```

**Python client example:**

```python
import asyncio
import websockets

async def stream_frames():
    uri = "ws://localhost:5100/ws/stream?mode=detect"
    headers = {"X-Api-Key": "your-api-key"}
    
    async with websockets.connect(uri, additional_headers=headers) as ws:
        with open("frame.jpg", "rb") as f:
            frame_bytes = f.read()
        
        # Send binary frame
        await ws.send(frame_bytes)
        
        # Receive JSON result
        result = await ws.recv()
        print(result)

asyncio.run(stream_frames())
```

> **Note:** The service enforces a minimum interval between AI calls (`Performance:MinAiIntervalMs`, default 500 ms) to prevent backend overload. Frames sent faster than this rate are throttled.

---

## Health Endpoints

### GET `/health`

Returns the combined health status of the service and its dependencies.

```bash
curl http://localhost:5100/health
```

**Response `200 OK` (healthy):**

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.045",
  "entries": {
    "yolo": { "status": "Healthy", "duration": "00:00:00.020" },
    "qwen-vl": { "status": "Healthy", "duration": "00:00:00.025" }
  }
}
```

**Response `503 Service Unavailable` (degraded):**

```json
{
  "status": "Degraded",
  "entries": {
    "yolo": { "status": "Unhealthy", "description": "Connection refused" },
    "qwen-vl": { "status": "Healthy" }
  }
}
```

---

### GET `/health/ready`

Returns `200` only when both AI backends are reachable. Used by Kubernetes readiness probes.

```bash
curl http://localhost:5100/health/ready
```

---

### GET `/health/live`

Always returns `200` if the .NET process is running. Used by Kubernetes liveness probes.

```bash
curl http://localhost:5100/health/live
```

---

## Metrics Endpoint

### GET `/metrics`

Exposes Prometheus-compatible metrics in the text exposition format.

```bash
curl http://localhost:5100/metrics
```

**Example output (abbreviated):**

```
# HELP http_requests_received_total Provides the count of HTTP requests that have been processed by the ASP.NET Core middleware
# TYPE http_requests_received_total counter
http_requests_received_total{code="200",method="POST",controller="",action=""} 142

# HELP http_request_duration_seconds The duration of HTTP requests processed by an ASP.NET Core application.
# TYPE http_request_duration_seconds histogram
http_request_duration_seconds_bucket{le="0.1"} 98
http_request_duration_seconds_bucket{le="0.5"} 136
http_request_duration_seconds_bucket{le="1"} 141
```

---

## Error Responses

All errors follow the [RFC 7807 ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807) format.

**400 Bad Request — validation failure:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "confidence must be between 0.0 and 1.0"
}
```

**401 Unauthorized — missing or invalid API key:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Missing or invalid X-Api-Key header"
}
```

**403 Forbidden — insufficient scope:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "API key does not have the required scope"
}
```

**429 Too Many Requests — rate limit exceeded:**

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Retry after 1 second."
}
```

**503 Service Unavailable — AI backend unreachable or circuit open:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "YOLO backend unavailable: Connection refused (yolo-api:7860)"
}
```

**500 Internal Server Error — unexpected error:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Check service logs for details.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```
