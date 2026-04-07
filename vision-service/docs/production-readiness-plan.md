# Vision Service — Production Readiness Plan

> **Goal:** Transform the existing VisionService microservice into a production-grade, deployable system.
> Each section below is a self-contained **GitHub Issue** designed for [Copilot Coding Agent](https://docs.github.com/en/copilot/using-github-copilot/using-copilot-coding-agent).
> Create each issue in order — later PRs depend on earlier ones merging first.

---

## Current State Summary

| Area | Status |
|---|---|
| Minimal API with endpoint groups | ✅ Done |
| YOLO + Qwen-VL HTTP clients | ✅ Done |
| IOptions configuration pattern | ✅ Done |
| Serilog structured logging | ✅ Done |
| Prometheus metrics | ✅ Done |
| Swagger / OpenAPI | ✅ Done |
| API Key auth middleware | ✅ Done |
| Rate limiting middleware | ✅ Done |
| Security headers middleware | ✅ Done |
| Correlation ID middleware | ✅ Done |
| Global exception handler (RFC 7807) | ✅ Done |
| Response compression | ✅ Done |
| WebSocket streaming | ✅ Done |
| Background jobs (cleanup, health) | ✅ Done |
| In-process event bus | ✅ Done |
| Response caching | ✅ Done |
| Pipeline orchestration endpoints | ✅ Done |
| Admin / playground endpoints | ✅ Done |
| Unit & integration test scaffolding | ✅ Done |
| .NET MAUI desktop client | ✅ Done |
| **Dockerfile** | ❌ Missing |
| **docker-compose.yml** | ❌ Missing |
| **CI/CD pipeline** | ❌ Missing |
| **Polly resilience on HTTP clients** | ❌ Missing (packages referenced) |
| **ASP.NET Core Health Checks** | ❌ Using custom endpoint |
| **OpenTelemetry tracing** | ❌ Packages referenced, not wired |
| **CORS configuration** | ❌ Missing |
| **appsettings.Production.json** | ❌ Missing |
| **WebSocket authentication** | ❌ Missing |
| **Graceful shutdown** | ❌ Missing |
| **Full test coverage** | ❌ Partial |
| **README & deployment docs** | ❌ Missing |
| **Kubernetes manifests** | ❌ Missing |

---

## Issue 1 — Docker Infrastructure (Dockerfile + docker-compose)

**Title:** `feat: Add Dockerfile and docker-compose.yml for full-stack local deployment`

**Labels:** `infrastructure`, `docker`

**Body:**

### Context
The VisionService (.NET 8 Minimal API) needs container infrastructure to run alongside its YOLO and Qwen-VL AI backends. There is currently no Dockerfile or docker-compose.yml in the repository.

### Requirements

1. **Create `docker/Dockerfile`** — multi-stage build for the .NET service:
   - Stage 1 (`build`): Use `mcr.microsoft.com/dotnet/sdk:8.0` to restore + publish in Release mode
   - Stage 2 (`runtime`): Use `mcr.microsoft.com/dotnet/aspnet:8.0`, expose port 5100, set `ASPNETCORE_URLS=http://+:5100`
   - Copy only `src/VisionService/` project (not tests or MAUI client)
   - Set `DOTNET_EnableDiagnostics=0` for smaller attack surface
   - Run as non-root user

2. **Create `docker/.dockerignore`** with entries for `bin/`, `obj/`, `*.md`, `.git/`, `maui-client/`, `node_modules/`, `*.user`, `*.suo`

3. **Create `docker-compose.yml`** at repo root:
   - `vision-service`: builds from `docker/Dockerfile`, ports `5100:5100`, depends on `yolo-api` and `qwen-vl`, volume mount for `/data/images`, restart `unless-stopped`
   - `yolo-api`: image placeholder `ghcr.io/wallywest21/yolo-api:latest`, port `7860`, GPU reservation via `deploy.resources.reservations.devices`
   - `qwen-vl`: image placeholder `ghcr.io/wallywest21/qwen-vl:latest`, port `8000`, GPU reservation
   - Shared Docker network `vision-net` (bridge)
   - Named volume `image-storage` for `/data/images`

4. **Create `docker-compose.override.yml`** for local development:
   - Mount `src/VisionService/appsettings.json` into container
   - Set `ASPNETCORE_ENVIRONMENT=Development`

### Acceptance Criteria
- [ ] `docker compose build` succeeds
- [ ] `docker compose config` shows valid YAML
- [ ] Service container starts and responds on `http://localhost:5100/health`
- [ ] All existing `dotnet test` continue to pass

### Files to Create/Modify
- `docker/Dockerfile` (new)
- `docker/.dockerignore` (new)
- `docker-compose.yml` (new)
- `docker-compose.override.yml` (new)

---

## Issue 2 — CI/CD Pipeline (GitHub Actions)

**Title:** `ci: Add GitHub Actions workflows for build, test, and Docker image push`

**Labels:** `ci/cd`, `github-actions`

**Body:**

### Context
The project has no CI/CD pipeline. Every PR must compile and pass tests before merging.

### Requirements

1. **Create `.github/workflows/ci.yml`** — triggers on push to `main` and all PRs:
   - Job `build-and-test`:
     - Checkout, setup .NET 8 SDK
     - `dotnet restore`
     - `dotnet build --no-restore --configuration Release`
     - `dotnet test --no-build --configuration Release --logger trx --results-directory TestResults`
     - Upload test results as artifact

2. **Create `.github/workflows/docker-publish.yml`** — triggers on push to `main` and version tags (`v*`):
   - Job `build-and-push`:
     - Checkout
     - Login to GitHub Container Registry (`ghcr.io`)
     - Build Docker image using `docker/Dockerfile`
     - Tag as `ghcr.io/wallywest21/vision-service:latest` and `ghcr.io/wallywest21/vision-service:<sha>` (and `:<tag>` on tag push)
     - Push to GHCR
   - Only push on `main` branch (not PRs)

3. **Create `.github/dependabot.yml`** for NuGet and GitHub Actions auto-updates (weekly schedule)

### Acceptance Criteria
- [ ] CI workflow runs on PRs and reports build + test status
- [ ] Docker publish workflow builds image on main branch push
- [ ] Dependabot config is valid YAML
- [ ] All existing `dotnet test` pass in CI

### Files to Create
- `.github/workflows/ci.yml` (new)
- `.github/workflows/docker-publish.yml` (new)
- `.github/dependabot.yml` (new)

---

## Issue 3 — Polly Resilience Policies on HTTP Clients

**Title:** `feat: Add Polly retry and circuit breaker policies to YOLO and Qwen-VL HTTP clients`

**Labels:** `resilience`, `backend`

**Body:**

### Context
The `VisionService.csproj` already references `Microsoft.Extensions.Http.Polly`, `Polly`, and `Polly.Extensions.Http`, but no resilience policies are configured on the `HttpClient` registrations in `ServiceCollectionExtensions.cs`. Backend AI services will experience transient failures in production.

### Requirements

1. **Modify `src/VisionService/Extensions/ServiceCollectionExtensions.cs`**:
   - Add a **retry policy** to both `AddYoloClient` and `AddQwenVlClient` HttpClient registrations:
     - Retry on `HttpRequestException` and transient HTTP errors (5xx, 408)
     - 3 retries with exponential backoff (2s, 4s, 8s) + jitter
     - Log each retry attempt using `ILogger`
   - Add a **circuit breaker policy** chained after retry:
     - Break after 5 consecutive failures
     - Break duration: 30 seconds
     - Log circuit state transitions (open/half-open/closed)
   - Use `Polly.Extensions.Http.HttpPolicyExtensions.HandleTransientHttpError()` as the base selector
   - Read retry count and circuit breaker settings from `YoloOptions` and `QwenVlOptions` respectively

2. **Modify `src/VisionService/Configuration/YoloOptions.cs`** — add properties:
   - `MaxRetries` (int, default 3) — already exists, verify it's used
   - `CircuitBreakerThreshold` (int, default 5)
   - `CircuitBreakerDurationSeconds` (int, default 30)

3. **Modify `src/VisionService/Configuration/QwenVlOptions.cs`** — add same circuit breaker properties

4. **Update `src/VisionService/appsettings.json`** — add circuit breaker defaults to `Yolo` and `QwenVl` sections

5. **Add tests in `src/VisionService.Tests/Clients/`**:
   - Test that retry policy retries on transient errors
   - Test that circuit breaker opens after threshold failures
   - Use `NSubstitute` or test `DelegatingHandler` to simulate failures

### Acceptance Criteria
- [ ] HTTP clients retry transient failures with exponential backoff
- [ ] Circuit breaker opens after consecutive failures and resets after duration
- [ ] Retry and circuit breaker settings are configurable via `appsettings.json`
- [ ] At least 2 new tests (happy retry + circuit breaker open)
- [ ] All existing tests pass

### Files to Modify
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
- `src/VisionService/Configuration/YoloOptions.cs`
- `src/VisionService/Configuration/QwenVlOptions.cs`
- `src/VisionService/appsettings.json`

### Files to Create
- `src/VisionService.Tests/Clients/PollyResilienceTests.cs` (new)

---

## Issue 4 — ASP.NET Core Health Checks with Backend Probes

**Title:** `feat: Replace custom /health endpoint with ASP.NET Core Health Checks including YOLO and Qwen-VL probes`

**Labels:** `health-checks`, `observability`

**Body:**

### Context
The current `/health` endpoint is a simple `MapGet` returning a static object. Production systems need proper health checks that probe downstream dependencies (YOLO API, Qwen-VL API) and report structured status.

### Requirements

1. **Modify `src/VisionService/Program.cs`**:
   - Register `builder.Services.AddHealthChecks()` with custom health checks for YOLO and Qwen-VL backends
   - Replace the existing `app.MapGet("/health", ...)` with `app.MapHealthChecks("/health")` using a custom JSON response writer
   - Add a separate `/health/ready` readiness endpoint (checks backends) and `/health/live` liveness endpoint (always healthy if process is running)

2. **Create `src/VisionService/HealthChecks/YoloHealthCheck.cs`**:
   - Implement `IHealthCheck`
   - Use `IYoloClient.IsHealthyAsync()` (already exists on the interface)
   - Return `HealthCheckResult.Healthy/Unhealthy/Degraded`

3. **Create `src/VisionService/HealthChecks/QwenVlHealthCheck.cs`**:
   - Same pattern using `IQwenVlClient.IsHealthyAsync()`

4. **Create `src/VisionService/HealthChecks/HealthCheckResponseWriter.cs`**:
   - Write structured JSON response with each check's name, status, duration, and optional exception message
   - Include overall status, service name, version, and timestamp

5. **Add tests**:
   - `GET /health/live` returns 200 always
   - `GET /health/ready` returns 200 when backends are healthy
   - `GET /health/ready` returns 503 when a backend is unhealthy

### Acceptance Criteria
- [ ] `/health/live` returns 200 and `Healthy` status
- [ ] `/health/ready` probes both AI backends
- [ ] `/health` returns aggregate JSON with individual check results
- [ ] Health check response includes service name, version, timestamp
- [ ] At least 3 new tests
- [ ] All existing tests pass (update any tests referencing old `/health` response shape)

### Files to Create
- `src/VisionService/HealthChecks/YoloHealthCheck.cs` (new)
- `src/VisionService/HealthChecks/QwenVlHealthCheck.cs` (new)
- `src/VisionService/HealthChecks/HealthCheckResponseWriter.cs` (new)
- `src/VisionService.Tests/HealthChecks/HealthCheckTests.cs` (new)

### Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService.Tests/HealthEndpointTests.cs` (update assertions)

---

## Issue 5 — OpenTelemetry Distributed Tracing

**Title:** `feat: Wire up OpenTelemetry distributed tracing for ASP.NET Core and HTTP clients`

**Labels:** `observability`, `tracing`

**Body:**

### Context
`VisionService.csproj` already references `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, and `OpenTelemetry.Instrumentation.Http`, but none are configured in `Program.cs`. Production needs distributed tracing across the service and its HTTP calls to YOLO/Qwen-VL backends.

### Requirements

1. **Modify `src/VisionService/Program.cs`**:
   - Add OpenTelemetry tracing configuration after `builder.Services.AddVisionServices(...)`:
     ```
     builder.Services.AddOpenTelemetry()
         .WithTracing(tracing => tracing
             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VisionService"))
             .AddAspNetCoreInstrumentation()
             .AddHttpClientInstrumentation()
             .AddOtlpExporter() // configurable via env OTEL_EXPORTER_OTLP_ENDPOINT
         );
     ```
   - Make the OTLP endpoint configurable via environment variable (default: `http://localhost:4317`)
   - Conditionally add a console exporter when `ASPNETCORE_ENVIRONMENT=Development`

2. **Add `OpenTelemetry.Exporter.OpenTelemetryProtocol` NuGet package** to `VisionService.csproj` (the OTLP exporter is not yet referenced)

3. **Add custom `ActivitySource`** in key services:
   - Create `src/VisionService/Diagnostics/VisionActivitySource.cs` with a static `ActivitySource` named `"VisionService"`
   - Add manual spans in `YoloClient.DetectAsync`, `QwenVlClient.CaptionAsync`, and `PipelineEndpoints.DetectAndDescribeAsync` to trace AI backend call durations

4. **Update `docker-compose.yml`** (if it exists from Issue 1):
   - Add optional `jaeger` service for local trace viewing: `jaegertracing/all-in-one:latest`, ports `16686:16686` (UI) and `4317:4317` (OTLP gRPC)
   - Set `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317` on the `vision-service` container

### Acceptance Criteria
- [ ] ASP.NET Core requests create trace spans
- [ ] HTTP client calls to YOLO/Qwen-VL create child spans
- [ ] Custom activity spans exist for key AI operations
- [ ] OTLP endpoint is configurable via environment
- [ ] All existing tests pass
- [ ] Jaeger UI shows traces in local docker-compose setup

### Files to Create
- `src/VisionService/Diagnostics/VisionActivitySource.cs` (new)

### Files to Modify
- `src/VisionService/VisionService.csproj`
- `src/VisionService/Program.cs`
- `src/VisionService/Clients/YoloClient.cs`
- `src/VisionService/Clients/QwenVlClient.cs`
- `src/VisionService/Endpoints/PipelineEndpoints.cs`
- `docker-compose.yml` (if exists)

---

## Issue 6 — CORS, Production Configuration, and Environment Variables

**Title:** `feat: Add CORS policy, appsettings.Production.json, and environment variable overrides`

**Labels:** `configuration`, `security`

**Body:**

### Context
No CORS policy is configured (the MAUI client makes HTTP calls), and there's no production-specific configuration file. Secrets are hardcoded in `appsettings.json`.

### Requirements

1. **Modify `src/VisionService/Program.cs`**:
   - Add CORS service with a named policy `"VisionCors"`:
     - Allow origins from a configurable list (`Cors:AllowedOrigins` in appsettings)
     - Allow any header and method
     - Expose `X-Correlation-Id` header
   - Apply CORS middleware after `UseResponseCompression` and before custom middleware

2. **Create `src/VisionService/Configuration/CorsOptions.cs`**:
   - `AllowedOrigins` (string[], default `["*"]`)
   - Register in `ServiceCollectionExtensions.cs`

3. **Create `src/VisionService/appsettings.Production.json`**:
   - Serilog minimum level: `Warning` for default, `Error` for Microsoft.AspNetCore
   - Auth.Enabled: `true`
   - RateLimit.RequestsPerMinute: `600`
   - RateLimit.BurstSize: `50`
   - Cache.DefaultTtlSeconds: `600`
   - Cors.AllowedOrigins: `[]` (must be explicitly set in deployment)
   - Comment out or remove sensitive defaults

4. **Document environment variable overrides** in `src/VisionService/appsettings.json` as comments or in a separate `docs/environment-variables.md`:
   - `Yolo__BaseUrl`, `QwenVl__BaseUrl`, `Auth__ApiKeys__0__Key`, `Storage__ImageStoragePath`, etc.
   - `ASPNETCORE_ENVIRONMENT`, `OTEL_EXPORTER_OTLP_ENDPOINT`

### Acceptance Criteria
- [ ] CORS headers present on API responses when origin matches
- [ ] `appsettings.Production.json` overrides appropriate defaults
- [ ] Environment variable override documentation exists
- [ ] All existing tests pass

### Files to Create
- `src/VisionService/Configuration/CorsOptions.cs` (new)
- `src/VisionService/appsettings.Production.json` (new)
- `docs/environment-variables.md` (new)

### Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`

---

## Issue 7 — Request Validation and File Upload Hardening

**Title:** `feat: Add input validation, file type verification, and request size limits`

**Labels:** `security`, `validation`

**Body:**

### Context
Endpoints accept file uploads without verifying magic bytes, and confidence/query parameters have minimal validation. Production needs defense-in-depth input validation.

### Requirements

1. **Create `src/VisionService/Services/FileValidationService.cs`**:
   - Validate file extensions against `StorageOptions.AllowedExtensions`
   - Validate file magic bytes (JPEG: `FF D8 FF`, PNG: `89 50 4E 47`, WebP: `52 49 46 46`, BMP: `42 4D`, GIF: `47 49 46 38`)
   - Validate file size against `StorageOptions.MaxFileSizeMb`
   - Return typed validation result (not exceptions)

2. **Create `src/VisionService/Services/IFileValidationService.cs`** — interface

3. **Modify all endpoints that accept `IFormFile`** to validate files before processing:
   - `YoloEndpoints` (detect, segment, classify, pose, batch)
   - `QwenVlEndpoints` (ask, caption, ocr, analyze, compare)
   - `PipelineEndpoints` (detect-and-describe, safety-check, inventory, scene)
   - `PlaygroundEndpoints` (playground)
   - Return `Results.Problem("...", statusCode: 400)` on validation failure

4. **Modify `src/VisionService/Program.cs`** — configure Kestrel request size limits:
   - `builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 20 * 1024 * 1024);`
   - (Matches `StorageOptions.MaxFileSizeMb` default of 20)

5. **Register `IFileValidationService` in `ServiceCollectionExtensions.cs`**

6. **Add tests**:
   - Valid JPEG passes validation
   - Invalid extension is rejected
   - Oversized file is rejected
   - Wrong magic bytes for declared extension is rejected

### Acceptance Criteria
- [ ] Uploads with invalid extensions return 400
- [ ] Uploads with mismatched magic bytes return 400
- [ ] Uploads exceeding size limit return 400
- [ ] At least 4 new validation tests
- [ ] All existing tests pass

### Files to Create
- `src/VisionService/Services/IFileValidationService.cs` (new)
- `src/VisionService/Services/FileValidationService.cs` (new)
- `src/VisionService.Tests/Services/FileValidationServiceTests.cs` (new)

### Files to Modify
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
- `src/VisionService/Endpoints/YoloEndpoints.cs`
- `src/VisionService/Endpoints/QwenVlEndpoints.cs`
- `src/VisionService/Endpoints/PipelineEndpoints.cs`
- `src/VisionService/Endpoints/PlaygroundEndpoints.cs`
- `src/VisionService/Program.cs`

---

## Issue 8 — Graceful Shutdown and Readiness/Liveness Probes

**Title:** `feat: Add graceful shutdown handling and Kubernetes-compatible probes`

**Labels:** `infrastructure`, `reliability`

**Body:**

### Context
The service needs to drain in-flight requests and close WebSocket connections before shutting down, and expose probes compatible with Kubernetes deployment.

### Requirements

1. **Modify `src/VisionService/Program.cs`**:
   - Register `IHostApplicationLifetime` shutdown hook:
     - Log "Shutting down gracefully..." on `ApplicationStopping`
     - Allow 10s drain period via `builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10))`
     - Log "Shutdown complete" on `ApplicationStopped`

2. **Modify `src/VisionService/Endpoints/WebSocketEndpoints.cs`**:
   - Accept `CancellationToken` from `HttpContext.RequestAborted` (already used)
   - Also wire `IHostApplicationLifetime.ApplicationStopping` token so WebSocket loops exit on shutdown
   - Send close frame before exiting

3. **Modify `src/VisionService/Jobs/ModelHealthCheckJob.cs` and `ImageCleanupJob.cs`**:
   - Ensure `stoppingToken` is respected in all loops (already done — verify and add logging on exit)
   - Log "Job stopping due to shutdown" when token fires

4. **Ensure health check probes from Issue 4 are Kubernetes-compatible**:
   - `/health/live` → liveness probe (always healthy if process alive)
   - `/health/ready` → readiness probe (healthy when backends reachable)

### Acceptance Criteria
- [ ] Service logs shutdown events on SIGTERM
- [ ] WebSocket connections receive close frame during shutdown
- [ ] Background jobs stop cleanly on shutdown
- [ ] Shutdown timeout is configurable
- [ ] All existing tests pass

### Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- `src/VisionService/Jobs/ModelHealthCheckJob.cs`
- `src/VisionService/Jobs/ImageCleanupJob.cs`

---

## Issue 9 — WebSocket Authentication and Connection Hardening

**Title:** `feat: Add API key authentication and connection limits to WebSocket endpoint`

**Labels:** `security`, `websocket`

**Body:**

### Context
The `/ws/stream` WebSocket endpoint bypasses `ApiKeyMiddleware` because WebSocket upgrade requests don't flow through the same middleware for responses. Additionally, there are no connection limits.

### Requirements

1. **Modify `src/VisionService/Endpoints/WebSocketEndpoints.cs`**:
   - Before accepting the WebSocket, validate API key from either:
     - `X-Api-Key` header, OR
     - `?apiKey=` query parameter (since browsers can't set headers on WebSocket)
   - Reject with 401 if auth is enabled and key is invalid
   - Add a `SemaphoreSlim` to limit concurrent WebSocket connections (configurable via `PerformanceOptions.MaxWebSocketConnections`, default 10)
   - Return 503 if connection limit reached

2. **Modify `src/VisionService/Configuration/PerformanceOptions.cs`**:
   - Add `MaxWebSocketConnections` (int, default 10)

3. **Update `src/VisionService/appsettings.json`** — add `MaxWebSocketConnections` to `Performance` section

4. **Add tests**:
   - WebSocket with valid API key connects successfully
   - WebSocket without API key returns 401 (when auth enabled)
   - WebSocket connection rejected when limit reached

### Acceptance Criteria
- [ ] WebSocket endpoint requires API key when auth is enabled
- [ ] Connection limit enforced with 503 response
- [ ] Existing WebSocket functionality works for authenticated clients
- [ ] At least 2 new tests
- [ ] All existing tests pass

### Files to Modify
- `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- `src/VisionService/Configuration/PerformanceOptions.cs`
- `src/VisionService/appsettings.json`

### Files to Create
- `src/VisionService.Tests/Endpoints/WebSocketEndpointTests.cs` (new)

---

## Issue 10 — Comprehensive Test Coverage

**Title:** `test: Add missing unit and integration tests for full coverage`

**Labels:** `testing`

**Body:**

### Context
Current test coverage is partial. The following endpoint/service groups lack adequate testing: Qwen-VL endpoints, Pipeline endpoints, Playground endpoints, WebSocket endpoints, Admin endpoints (partial), and client resilience behavior.

### Requirements

1. **Expand `src/VisionService.Tests/Endpoints/QwenVlEndpointTests.cs`**:
   - Test `POST /api/v1/ask` with valid image → 200
   - Test `POST /api/v1/caption` with valid image → 200
   - Test `POST /api/v1/ocr` with valid image → 200
   - Test when Qwen-VL backend is unavailable → 503

2. **Expand `src/VisionService.Tests/Endpoints/PipelineEndpointTests.cs`**:
   - Test `POST /api/v1/pipeline/detect-and-describe` → 200
   - Test `POST /api/v1/pipeline/safety-check` → 200
   - Test backend failure → 503

3. **Create `src/VisionService.Tests/Endpoints/PlaygroundEndpointTests.cs`**:
   - Test `POST /api/v1/playground` → 200 with mocked clients
   - Test backend failure → 503

4. **Expand `src/VisionService.Tests/Endpoints/AdminEndpointTests.cs`**:
   - Test `GET /api/v1/admin/settings` → 200 with expected shape
   - Test `PUT /api/v1/admin/settings` → 200 and values updated
   - Test `POST /api/v1/admin/keys` with missing name → 400

5. **Create `src/VisionService.Tests/Clients/QwenVlClientTests.cs`**:
   - Test `CaptionAsync` with successful HTTP response
   - Test `AskAsync` with backend failure throws `HttpRequestException`

6. **All test classes MUST follow conventions**:
   - Test framework: xUnit
   - Mocking: NSubstitute
   - Assertions: FluentAssertions
   - Naming: `MethodName_Scenario_ExpectedResult`
   - At least one happy path + one failure path per endpoint

### Acceptance Criteria
- [ ] All listed test methods exist and pass
- [ ] Every endpoint has at least one happy path and one failure test
- [ ] `dotnet test` runs green with no skipped tests
- [ ] No `// TODO` comments in test files

### Files to Create
- `src/VisionService.Tests/Endpoints/PlaygroundEndpointTests.cs` (new)
- `src/VisionService.Tests/Clients/QwenVlClientTests.cs` (new)

### Files to Modify
- `src/VisionService.Tests/Endpoints/QwenVlEndpointTests.cs`
- `src/VisionService.Tests/Endpoints/PipelineEndpointTests.cs`
- `src/VisionService.Tests/Endpoints/AdminEndpointTests.cs`

---

## Issue 11 — README and Deployment Documentation

**Title:** `docs: Add comprehensive README, API reference, and deployment guide`

**Labels:** `documentation`

**Body:**

### Context
The repository has no main README or deployment documentation. Contributors and operators need clear instructions.

### Requirements

1. **Create `README.md`** at repo root:
   - Project description: Camera vision microservice orchestrating YOLOv8 + Qwen-VL
   - Architecture diagram (Mermaid) showing: MAUI Client → VisionService → YOLO API / Qwen-VL
   - Quick start with `docker compose up`
   - API overview table: endpoint, method, description
   - Configuration reference: all options sections with defaults
   - Link to Swagger UI at `/swagger`
   - Development setup instructions (prerequisites, build, test)
   - License section

2. **Create `docs/deployment-guide.md`**:
   - Docker Compose deployment (local + server)
   - Kubernetes deployment (reference manifests from Issue 12)
   - Environment variables reference (from Issue 6)
   - GPU setup for YOLO and Qwen-VL containers
   - TLS termination recommendations (reverse proxy)
   - Monitoring: Prometheus scrape config, Grafana dashboard tips, Jaeger trace viewing
   - Backup and retention: image storage volume, cleanup job configuration

3. **Create `docs/api-reference.md`**:
   - All endpoints grouped by tag (YOLO, Qwen-VL, Pipeline, Admin, Playground, WebSocket)
   - Request/response examples with `curl`
   - Authentication header usage
   - WebSocket connection example with `websocat`

### Acceptance Criteria
- [ ] `README.md` exists at repo root with all sections
- [ ] `docs/deployment-guide.md` covers Docker + K8s deployment
- [ ] `docs/api-reference.md` has examples for every endpoint
- [ ] No broken markdown links
- [ ] All existing tests pass

### Files to Create
- `README.md` (new)
- `docs/deployment-guide.md` (new)
- `docs/api-reference.md` (new)

---

## Issue 12 — Kubernetes Deployment Manifests

**Title:** `feat: Add Kubernetes deployment manifests for production deployment`

**Labels:** `infrastructure`, `kubernetes`

**Body:**

### Context
For production deployment beyond Docker Compose, the service needs Kubernetes manifests with proper resource limits, probes, and scaling configuration.

### Requirements

1. **Create `k8s/namespace.yml`** — `vision-system` namespace

2. **Create `k8s/vision-service/deployment.yml`**:
   - 2 replicas (HPA scales 2–10)
   - Container: `ghcr.io/wallywest21/vision-service:latest`
   - Resource requests: 256Mi memory, 250m CPU
   - Resource limits: 512Mi memory, 500m CPU
   - Liveness probe: `GET /health/live` every 10s
   - Readiness probe: `GET /health/ready` every 15s with 30s initial delay
   - Environment variables from ConfigMap and Secret
   - Volume mount for image storage (PVC)

3. **Create `k8s/vision-service/service.yml`** — ClusterIP service on port 5100

4. **Create `k8s/vision-service/configmap.yml`** — non-secret configuration (BaseUrls, rate limits, cache settings)

5. **Create `k8s/vision-service/secret.yml`** — template for API keys (values as `<BASE64_ENCODED>` placeholders)

6. **Create `k8s/vision-service/hpa.yml`** — HorizontalPodAutoscaler targeting 70% CPU

7. **Create `k8s/vision-service/pvc.yml`** — PersistentVolumeClaim for image storage (10Gi)

8. **Create `k8s/vision-service/ingress.yml`** — Ingress with TLS annotation placeholders for nginx-ingress

### Acceptance Criteria
- [ ] `kubectl apply -f k8s/ --dry-run=client` succeeds
- [ ] All manifests use consistent labels (`app: vision-service`)
- [ ] Probes reference correct health check paths
- [ ] Secret template contains only placeholders (no real values)
- [ ] All existing tests pass

### Files to Create
- `k8s/namespace.yml` (new)
- `k8s/vision-service/deployment.yml` (new)
- `k8s/vision-service/service.yml` (new)
- `k8s/vision-service/configmap.yml` (new)
- `k8s/vision-service/secret.yml` (new)
- `k8s/vision-service/hpa.yml` (new)
- `k8s/vision-service/pvc.yml` (new)
- `k8s/vision-service/ingress.yml` (new)

---

## Dependency Graph

```
Issue 1 (Docker)
  └─► Issue 2 (CI/CD) ─── can start after Issue 1
  └─► Issue 5 (OpenTelemetry) ─── updates docker-compose

Issue 3 (Polly) ─── independent, can start immediately
Issue 4 (Health Checks) ─── independent, can start immediately
  └─► Issue 8 (Graceful Shutdown) ─── depends on Issue 4 for probes
  └─► Issue 12 (Kubernetes) ─── depends on Issue 4 for probe paths

Issue 6 (CORS/Config) ─── independent, can start immediately
Issue 7 (Validation) ─── independent, can start immediately
Issue 9 (WS Auth) ─── independent, can start immediately

Issue 10 (Tests) ─── should come after Issues 3,4,7,9 (tests the new features)
Issue 11 (Docs) ─── should come last (documents everything)
Issue 12 (K8s) ─── depends on Issues 1,4,8
```

### Recommended execution order:
1. Issue 1 — Docker Infrastructure
2. Issue 3 — Polly Resilience (parallel with Issue 1)
3. Issue 4 — Health Checks (parallel with Issue 1)
4. Issue 2 — CI/CD Pipeline
5. Issue 5 — OpenTelemetry Tracing
6. Issue 6 — CORS & Production Config
7. Issue 7 — Request Validation
8. Issue 9 — WebSocket Auth
9. Issue 8 — Graceful Shutdown
10. Issue 10 — Comprehensive Tests
11. Issue 12 — Kubernetes Manifests
12. Issue 11 — README & Documentation

---

## How to Use with Copilot Coding Agent

1. Go to your repo's **Issues** tab on GitHub
2. Create a **new issue** for each section above (Issue 1 through Issue 12)
3. Copy the **Title** as the issue title
4. Copy the **Body** (everything between `### Context` and `### Files to ...`) as the issue body
5. Add the **Labels** listed for each issue
6. **Assign** each issue to **Copilot** (the coding agent)
7. Copilot will automatically create a PR for each issue
8. Review and merge PRs in the recommended order above

> **Tip:** Create issues 1, 3, 4, and 6 first — they have no dependencies and Copilot can work on them in parallel.
