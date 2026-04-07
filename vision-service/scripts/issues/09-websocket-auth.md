## Context
The `/ws/stream` WebSocket endpoint bypasses `ApiKeyMiddleware` because WebSocket upgrade requests do not flow through the same middleware for responses. Additionally, there are no connection limits.

## Requirements

### 1. Modify `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- Before accepting the WebSocket, validate API key from either:
  - `X-Api-Key` header, OR
  - `?apiKey=` query parameter (since browsers cannot set headers on WebSocket)
- Reject with 401 if auth is enabled and key is invalid
- Add a `SemaphoreSlim` to limit concurrent WebSocket connections (configurable via `PerformanceOptions.MaxWebSocketConnections`, default 10)
- Return 503 if connection limit reached

### 2. Modify `src/VisionService/Configuration/PerformanceOptions.cs`
Add `MaxWebSocketConnections` (int, default 10)

### 3. Update `src/VisionService/appsettings.json`
Add `MaxWebSocketConnections` to `Performance` section.

### 4. Add tests
- WebSocket with valid API key connects successfully
- WebSocket without API key returns 401 (when auth enabled)
- WebSocket connection rejected when limit reached

## Acceptance Criteria
- [ ] WebSocket endpoint requires API key when auth is enabled
- [ ] Connection limit enforced with 503 response
- [ ] Existing WebSocket functionality works for authenticated clients
- [ ] At least 2 new tests
- [ ] All existing tests pass

## Files to Modify
- `src/VisionService/Endpoints/WebSocketEndpoints.cs`
- `src/VisionService/Configuration/PerformanceOptions.cs`
- `src/VisionService/appsettings.json`

## Files to Create
- `src/VisionService.Tests/Endpoints/WebSocketEndpointTests.cs` (new)
