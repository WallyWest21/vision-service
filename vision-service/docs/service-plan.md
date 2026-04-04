# Vision Microservice — Implementation Plan

## Overview

A production-ready .NET 8 microservice that orchestrates two AI backends — **YOLOv8** (object detection, segmentation, pose estimation, classification) and **Qwen-VL** (visual question answering, image captioning, OCR, scene understanding) — behind a unified REST + WebSocket API. The service runs alongside the AI containers in a Docker Compose stack.

---

## Architecture

```
Client (Jarvis / Browser / Mobile)
        │
        ▼
┌──────────────────────────────┐
│   VisionService (.NET 8)     │
│   Port 5100                  │
│                              │
│  ┌────────┐  ┌────────────┐  │
│  │ YOLO   │  │  Qwen-VL   │  │
│  │ Client │  │  Client    │  │
│  └───┬────┘  └─────┬──────┘  │
└──────┼─────────────┼─────────┘
       │             │
       ▼             ▼
  yolo-api:7860  qwen-vl:8000
  (Gradio/Fast)  (vLLM OpenAI)
```

---

## PR Breakdown — Implement in This Order

### PR 1 — Project Scaffold & Docker Infrastructure
- `VisionService.sln` solution file
- `src/VisionService/VisionService.csproj` — .NET 8 minimal API
- `src/VisionService/Program.cs` — basic health endpoint `/health`
- `src/VisionService.Tests/VisionService.Tests.csproj` — xUnit test project
- `docker/Dockerfile` for the .NET service
- `docker-compose.yml` at repo root with all 3 services (vision-service, yolo-api, qwen-vl)
- `docker-compose.override.yml` for local dev (volume mounts, debug ports)
- `.github/workflows/ci.yml` — build + test on every PR
- **Tests:** Health endpoint returns 200, DI container builds without error

### PR 2 — Configuration & Service Registration
- `src/VisionService/Configuration/YoloOptions.cs` — BaseUrl, TimeoutSeconds, MaxRetries
- `src/VisionService/Configuration/QwenVlOptions.cs` — BaseUrl, ModelName, MaxTokens, Temperature
- `src/VisionService/Configuration/StorageOptions.cs` — ImageStoragePath, MaxFileSizeMb, AllowedExtensions
- `appsettings.json` and `appsettings.Development.json` with defaults pointing to Docker service names
- Options pattern registration in `Program.cs` with validation
- **Tests:** Options binding, validation rejects bad config (missing URL, negative timeout)

### PR 3 — HTTP Clients for AI Backends
- `src/VisionService/Clients/IYoloClient.cs` — interface
- `src/VisionService/Clients/YoloClient.cs` — typed HttpClient calling yolo-api
  - `DetectAsync(Stream image, float confidence = 0.5)` → list of detections
  - `SegmentAsync(Stream image)` → segmentation mask + detections
  - `ClassifyAsync(Stream image)` → top-N classifications
  - `PoseAsync(Stream image)` → keypoints per person
- `src/VisionService/Clients/IQwenVlClient.cs` — interface
- `src/VisionService/Clients/QwenVlClient.cs` — typed HttpClient calling vLLM OpenAI-compatible endpoint
  - `AskAsync(Stream image, string question)` → text answer
  - `CaptionAsync(Stream image)` → description
  - `OcrAsync(Stream image)` → extracted text
  - `AnalyzeAsync(Stream image, string systemPrompt)` → structured analysis
- `src/VisionService/Models/` — DTOs: `Detection`, `BoundingBox`, `Keypoint`, `Segmentation`, `VlResponse`
- Polly retry + circuit breaker policies registered via `IHttpClientFactory`
- **Tests:** Unit tests with mocked HttpMessageHandler for each client method, deserialization tests for all DTOs

### PR 4 — Image Handling & Storage
- `src/VisionService/Services/IImageService.cs`
- `src/VisionService/Services/ImageService.cs`
  - `SaveAsync(IFormFile file)` → returns storage path + metadata
  - `LoadAsync(string imageId)` → returns Stream
  - `DeleteAsync(string imageId)`
  - `ResizeAsync(Stream image, int maxDim)` → resized Stream
  - `ConvertToBase64Async(Stream image)` → base64 string for Qwen-VL
  - Validates file type, size, dimensions
- File-based storage under `/data/images/{yyyy}/{MM}/{dd}/{guid}.{ext}`
- Automatic cleanup of images older than configurable retention days
- **Tests:** Save/load roundtrip, validation rejects oversized/wrong-type files, base64 conversion

### PR 5 — YOLO Detection Endpoints
- `POST /api/v1/detect` — upload image → detections with bounding boxes
- `POST /api/v1/detect/batch` — upload multiple images → batch results
- `POST /api/v1/segment` — upload image → instance segmentation masks
- `POST /api/v1/classify` — upload image → top-N classifications
- `POST /api/v1/pose` — upload image → pose keypoints
- All endpoints accept `multipart/form-data` or JSON with base64 image
- Query params: `confidence` (0.0–1.0), `classes` (filter), `maxDetections`
- Response includes processing time, model info, image dimensions
- **Tests:** Integration tests using test images, parameter validation, error responses for bad images

### PR 6 — Qwen-VL Vision-Language Endpoints
- `POST /api/v1/ask` — upload image + question → text answer
- `POST /api/v1/caption` — upload image → descriptive caption
- `POST /api/v1/ocr` — upload image → extracted text with regions
- `POST /api/v1/analyze` — upload image + custom system prompt → structured analysis
- `POST /api/v1/compare` — upload 2 images → comparison description
- `POST /api/v1/describe/detailed` — upload image → long-form scene description
- Query params: `maxTokens`, `temperature`, `language`
- **Tests:** Integration tests for each endpoint, prompt injection safety, token limit validation

### PR 7 — Combined / Pipeline Endpoints
- `POST /api/v1/pipeline/detect-and-describe` — YOLO detect → crop each detection → Qwen-VL describe each
- `POST /api/v1/pipeline/safety-check` — YOLO detect + Qwen-VL "is this safe?" analysis
- `POST /api/v1/pipeline/inventory` — YOLO detect + Qwen-VL classify/count items
- `POST /api/v1/pipeline/scene` — full scene: detections + caption + OCR combined
- Pipeline orchestration with parallel execution where possible
- **Tests:** Pipeline produces combined output, handles partial backend failures gracefully

### PR 8 — WebSocket Streaming & Live Camera Feed
- `GET /ws/stream` — WebSocket endpoint for real-time frame processing
  - Client sends frames as binary messages
  - Server responds with JSON detection/analysis results per frame
  - Configurable processing mode: `detect`, `caption`, `track`
- Frame rate throttling (configurable max FPS to process)
- Object tracking across frames (simple centroid tracker)
- Connection management: max concurrent streams, heartbeat, auto-disconnect
- **Tests:** WebSocket connection lifecycle, frame processing, throttling behavior

### PR 9 — Background Jobs & Event System
- `src/VisionService/Jobs/ImageCleanupJob.cs` — periodic cleanup of old stored images
- `src/VisionService/Jobs/ModelHealthCheckJob.cs` — periodic ping of YOLO + Qwen-VL health
- `src/VisionService/Events/IVisionEventBus.cs` — in-process event bus
- Events: `DetectionCompleted`, `AnalysisCompleted`, `BackendUnhealthy`, `BackendRecovered`
- Event handlers for logging, metrics, and webhook notifications
- **Tests:** Job scheduling, event publish/subscribe, cleanup removes only expired images

### PR 10 — Caching & Performance
- In-memory response cache with configurable TTL (image hash → results)
- Cache key generation from image content hash + parameters
- `Cache-Control` and `ETag` headers on responses
- Request deduplication — concurrent identical requests share one backend call
- Rate limiting per client IP (configurable via `RateLimitOptions`)
- Response compression (gzip/brotli)
- **Tests:** Cache hit/miss behavior, deduplication, rate limit enforcement

### PR 11 — Structured Logging, Metrics & Observability
- Serilog with structured JSON logging to console + file
- OpenTelemetry traces for each endpoint and backend call
- Prometheus metrics endpoint `/metrics`
  - `vision_requests_total` (counter, labels: endpoint, status)
  - `vision_request_duration_seconds` (histogram, labels: endpoint, backend)
  - `vision_backend_health` (gauge, labels: backend)
  - `vision_cache_hits_total` / `vision_cache_misses_total`
- Correlation ID middleware (X-Correlation-Id header)
- Request/response logging middleware (configurable verbosity)
- **Tests:** Metrics increment on requests, correlation ID propagates, log output structure

### PR 12 — Authentication & Authorization
- API key authentication via `X-Api-Key` header
- API key management: generate, revoke, list (stored in config or SQLite)
- Role-based scopes: `detect`, `analyze`, `admin`, `stream`
- Rate limit tiers per API key
- Admin endpoints secured separately: `GET /api/v1/admin/keys`, `POST /api/v1/admin/keys`
- **Tests:** Unauthenticated requests get 401, wrong scope gets 403, valid key passes

### PR 13 — OpenAPI Documentation & SDK
- Swagger / OpenAPI 3.0 spec auto-generated from endpoints
- XML documentation on all public types
- Example requests/responses in Swagger UI
- `POST /api/v1/playground` — interactive test endpoint for browser-based image uploads
- API versioning support (`/api/v1/`, `/api/v2/` ready)
- **Tests:** OpenAPI spec is valid, all endpoints documented, example values present

### PR 14 — Production Hardening
- Dockerfile multi-stage build optimized (trim, AOT-ready)
- `docker-compose.prod.yml` with resource limits, restart policies, healthchecks
- HTTPS/TLS termination support
- Graceful shutdown handling
- Global exception handler with ProblemDetails responses
- Request size limits and timeout middleware
- Security headers middleware (CORS, CSP, HSTS)
- **Tests:** Container builds and starts, healthcheck passes, graceful shutdown completes, error responses are ProblemDetails format

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| API Framework | .NET 8 Minimal API |
| YOLO Backend | ultralytics/yolov8 via FastAPI/Gradio (existing container) |
| VL Backend | Qwen-VL via vLLM OpenAI-compatible API (existing container) |
| HTTP Resilience | Polly v8 (retry, circuit breaker, timeout) |
| Caching | IMemoryCache + custom deduplication |
| Logging | Serilog + OpenTelemetry |
| Metrics | prometheus-net |
| Auth | Custom API key middleware |
| Testing | xUnit + FluentAssertions + NSubstitute + Testcontainers |
| Container | Docker + Docker Compose |
| CI/CD | GitHub Actions |

---

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `YOLO__BaseUrl` | `http://yolo-api:7860` | YOLO API base URL |
| `YOLO__TimeoutSeconds` | `30` | YOLO request timeout |
| `QWENVL__BaseUrl` | `http://qwen-vl:8000` | Qwen-VL vLLM base URL |
| `QWENVL__ModelName` | `Qwen/Qwen2.5-VL-7B-Instruct` | Model identifier |
| `QWENVL__MaxTokens` | `1024` | Max response tokens |
| `STORAGE__ImageStoragePath` | `/data/images` | Image storage root |
| `STORAGE__RetentionDays` | `7` | Auto-cleanup threshold |
| `AUTH__Enabled` | `true` | Enable API key auth |
| `RATELIMIT__RequestsPerMinute` | `60` | Default rate limit |

---

## Non-Functional Requirements

- Response time < 200ms for cached requests
- Support 50 concurrent WebSocket streams
- Zero-downtime deploys via Docker healthchecks
- All endpoints return `application/json` with consistent error schema
- 90%+ code coverage target
