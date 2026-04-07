## Context
The service needs to drain in-flight requests and close WebSocket connections before shutting down, and expose probes compatible with Kubernetes deployment.

## Requirements

### 1. Modify `src/VisionService/Program.cs`
- Register `IHostApplicationLifetime` shutdown hook:
  - Log 'Shutting down gracefully...' on `ApplicationStopping`
  - Allow 10s drain period via `builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10))`
  - Log 'Shutdown complete' on `ApplicationStopped`

### 2. Modify `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- Also wire `IHostApplicationLifetime.ApplicationStopping` token so WebSocket loops exit on shutdown
- Send close frame before exiting

### 3. Modify `src/VisionService/Jobs/ModelHealthCheckJob.cs` and `ImageCleanupJob.cs`
- Verify `stoppingToken` is respected in all loops
- Add logging on exit: 'Job stopping due to shutdown'

### 4. Ensure health check probes are Kubernetes-compatible
- `/health/live` -> liveness probe (always healthy if process alive)
- `/health/ready` -> readiness probe (healthy when backends reachable)

## Dependencies
> Depends on **Issue 4** (Health Checks) for probe endpoints.

## Acceptance Criteria
- [ ] Service logs shutdown events on SIGTERM
- [ ] WebSocket connections receive close frame during shutdown
- [ ] Background jobs stop cleanly on shutdown
- [ ] Shutdown timeout is configurable
- [ ] All existing tests pass

## Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- `src/VisionService/Jobs/ModelHealthCheckJob.cs`
- `src/VisionService/Jobs/ImageCleanupJob.cs`
