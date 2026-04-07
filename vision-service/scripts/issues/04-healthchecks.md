## Context
The current `/health` endpoint is a simple `MapGet` returning a static object. Production systems need proper health checks that probe downstream dependencies (YOLO API, Qwen-VL API) and report structured status.

## Requirements

### 1. Modify `src/VisionService/Program.cs`
- Register `builder.Services.AddHealthChecks()` with custom health checks for YOLO and Qwen-VL backends
- Replace the existing `app.MapGet("/health", ...)` with `app.MapHealthChecks("/health")` using a custom JSON response writer
- Add `/health/ready` readiness endpoint (checks backends) and `/health/live` liveness endpoint (always healthy if process is running)

### 2. Create `src/VisionService/HealthChecks/YoloHealthCheck.cs`
- Implement `IHealthCheck`
- Use `IYoloClient.IsHealthyAsync()` (already exists on the interface)
- Return `HealthCheckResult.Healthy/Unhealthy/Degraded`

### 3. Create `src/VisionService/HealthChecks/QwenVlHealthCheck.cs`
Same pattern using `IQwenVlClient.IsHealthyAsync()`

### 4. Create `src/VisionService/HealthChecks/HealthCheckResponseWriter.cs`
- Write structured JSON response with each check's name, status, duration, and optional exception message
- Include overall status, service name, version, and timestamp

### 5. Add tests
- `GET /health/live` returns 200 always
- `GET /health/ready` returns 200 when backends are healthy
- `GET /health/ready` returns 503 when a backend is unhealthy

## Acceptance Criteria
- [ ] `/health/live` returns 200 and `Healthy` status
- [ ] `/health/ready` probes both AI backends
- [ ] `/health` returns aggregate JSON with individual check results
- [ ] Health check response includes service name, version, timestamp
- [ ] At least 3 new tests
- [ ] All existing tests pass (update any tests referencing old `/health` response shape)

## Files to Create
- `src/VisionService/HealthChecks/YoloHealthCheck.cs` (new)
- `src/VisionService/HealthChecks/QwenVlHealthCheck.cs` (new)
- `src/VisionService/HealthChecks/HealthCheckResponseWriter.cs` (new)
- `src/VisionService.Tests/HealthChecks/HealthCheckTests.cs` (new)

## Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService.Tests/HealthEndpointTests.cs` (update assertions)
