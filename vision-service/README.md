# Vision Microservice

A production-ready .NET 8 microservice orchestrating YOLOv8 and Qwen-VL AI backends for camera vision tasks: object detection, segmentation, pose estimation, image captioning, VQA, OCR, and multi-model pipelines.

---

## Visual Studio 2026 Setup — From Zero to Copilot PRs

### Prerequisites

- **Visual Studio 2026** with these workloads:
  - ASP.NET and web development
  - .NET desktop development (for test runner)
- **GitHub Copilot** extension enabled and signed in (requires Copilot Business or Enterprise for coding agent)
- **Git** configured with your GitHub account
- **Docker Desktop** running (for local testing)

### Step-by-Step

#### 1. Create a New GitHub Repository

1. Go to [github.com/new](https://github.com/new)
2. Name it `vision-service` (or whatever you prefer)
3. Set it to **Private**
4. Do NOT initialize with README, .gitignore, or license — we're pushing our own
5. Click **Create repository**
6. Copy the repo URL (e.g., `https://github.com/YOUR_USERNAME/vision-service.git`)

#### 2. Download & Push These Files

**Option A — From Visual Studio:**

1. Open Visual Studio 2026
2. Click **Clone a repository** → paste your repo URL → Clone
3. This opens an empty repo folder
4. **Extract the downloaded files** from Claude into this folder so the structure looks like:

```
vision-service/
├── .github/
│   ├── copilot-instructions.md
│   └── workflows/
│       └── ci.yml
├── docker/
│   ├── Dockerfile
│   └── yolo/
│       ├── Dockerfile.yolo
│       ├── app.py
│       └── requirements.txt
├── docs/
│   └── service-plan.md
├── src/
│   ├── VisionService/
│   │   ├── VisionService.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   └── VisionService.Tests/
│       ├── VisionService.Tests.csproj
│       └── HealthEndpointTests.cs
├── .gitignore
├── COPILOT_PROMPT.md
├── README.md
├── VisionService.sln
└── docker-compose.yml
```

5. Open `VisionService.sln` in Visual Studio
6. In **Solution Explorer**, verify both projects load and restore NuGet packages
7. Build the solution: **Build → Build Solution** (Ctrl+Shift+B) — should succeed
8. Run tests: **Test → Run All Tests** — 2 tests should pass

#### 3. Initial Commit & Push

Open **Git Changes** panel in Visual Studio (View → Git Changes):

1. Stage all files
2. Commit message: `Initial scaffold: solution, Docker infrastructure, CI pipeline`
3. Click **Push** (or Commit + Push)

Verify on GitHub that all files appear in your repo.

#### 4. Enable GitHub Actions

1. Go to your repo on GitHub → **Settings → Actions → General**
2. Under "Workflow permissions", select **Read and write permissions**
3. Check **Allow GitHub Actions to create and approve pull requests**
4. Save

#### 5. Launch Copilot Coding Agent

1. Back in Visual Studio 2026, open the **Copilot Chat** panel
2. Switch to **Copilot Coding Agent** mode (the agent icon, not regular chat)
3. Open `COPILOT_PROMPT.md` and copy everything below the horizontal rule
4. Paste it into the Copilot coding agent input
5. Press Enter and **walk away**

Copilot will now:
- Read `docs/service-plan.md` and `.github/copilot-instructions.md`
- Create 14 feature branches
- Implement all code for each PR section
- Open 14 pull requests against `main`
- Each PR triggers the CI workflow automatically

#### 6. Review & Merge from GitHub Mobile

1. Open the **GitHub mobile app** on your phone
2. Go to your `vision-service` repo → **Pull Requests**
3. You'll see PRs appearing as Copilot creates them
4. For each PR:
   - Check that the CI status check is green ✅
   - Review the file changes
   - Tap **Merge pull request**
   - Use **Squash and merge** for clean history
5. Merge in order: PR 1 → PR 2 → ... → PR 14

---

## Running Locally After All PRs Merged

### Full stack with Docker Compose:

```bash
# Clone the final repo
git clone https://github.com/YOUR_USERNAME/vision-service.git
cd vision-service

# Start everything
docker compose up -d

# Check health
curl http://localhost:5100/health
curl http://localhost:7861/health
curl http://localhost:8001/health
```

### Just the .NET service (backends already running on your machine):

```bash
cd src/VisionService
dotnet run
```

This uses `appsettings.Development.json` which points to `localhost:7861` and `localhost:8001` — matching the ports exposed in your existing Docker stack.

### Test an endpoint:

```bash
# Object detection
curl -X POST http://localhost:5100/api/v1/detect \
  -F "file=@test-image.jpg" \
  -F "confidence=0.5"

# Image captioning
curl -X POST http://localhost:5100/api/v1/caption \
  -F "file=@test-image.jpg"

# Visual Q&A
curl -X POST http://localhost:5100/api/v1/ask \
  -F "file=@test-image.jpg" \
  -F "question=What objects are in this image?"

# Full scene pipeline
curl -X POST http://localhost:5100/api/v1/pipeline/scene \
  -F "file=@test-image.jpg"
```

### Swagger UI:

Open `http://localhost:5100/swagger` in your browser for the interactive API docs.

---

## Architecture

```
┌─────────────────────────────────────────────┐
│              Clients                         │
│  (Jarvis MK2 / Browser / Mobile / ESP32)    │
└──────────────────┬──────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│          VisionService (.NET 8)              │
│          Port 5100                           │
│                                              │
│  REST API ──────────────────── WebSocket     │
│  /api/v1/detect                /ws/stream    │
│  /api/v1/caption                             │
│  /api/v1/ask                                 │
│  /api/v1/pipeline/*                          │
│                                              │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐  │
│  │  Cache   │  │  Auth    │  │  Metrics  │  │
│  └──────────┘  └──────────┘  └───────────┘  │
└───────┬──────────────────────────┬───────────┘
        │                          │
        ▼                          ▼
  ┌───────────┐            ┌─────────────┐
  │ yolo-api  │            │  qwen-vl    │
  │ Port 7860 │            │  Port 8000  │
  │ YOLOv8    │            │  vLLM       │
  │ FastAPI   │            │  OpenAI API │
  └───────────┘            └─────────────┘
```

---

## Endpoints Summary

| Method | Path | Description | Backend |
|--------|------|-------------|---------|
| GET | `/health` | Service health check | — |
| GET | `/metrics` | Prometheus metrics | — |
| POST | `/api/v1/detect` | Object detection | YOLO |
| POST | `/api/v1/detect/batch` | Batch detection | YOLO |
| POST | `/api/v1/segment` | Instance segmentation | YOLO |
| POST | `/api/v1/classify` | Image classification | YOLO |
| POST | `/api/v1/pose` | Pose estimation | YOLO |
| POST | `/api/v1/ask` | Visual Q&A | Qwen-VL |
| POST | `/api/v1/caption` | Image captioning | Qwen-VL |
| POST | `/api/v1/ocr` | Text extraction | Qwen-VL |
| POST | `/api/v1/analyze` | Custom analysis | Qwen-VL |
| POST | `/api/v1/compare` | Image comparison | Qwen-VL |
| POST | `/api/v1/describe/detailed` | Long-form description | Qwen-VL |
| POST | `/api/v1/pipeline/detect-and-describe` | Detect → Describe each | Both |
| POST | `/api/v1/pipeline/safety-check` | Safety analysis | Both |
| POST | `/api/v1/pipeline/inventory` | Count & classify items | Both |
| POST | `/api/v1/pipeline/scene` | Full scene analysis | Both |
| WS | `/ws/stream` | Real-time frame processing | Both |
