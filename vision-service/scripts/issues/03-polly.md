## Context
The `VisionService.csproj` already references `Microsoft.Extensions.Http.Polly`, `Polly`, and `Polly.Extensions.Http`, but no resilience policies are configured on the `HttpClient` registrations in `ServiceCollectionExtensions.cs`. Backend AI services will experience transient failures in production.

## Requirements

### 1. Modify `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
- Add a **retry policy** to both `AddYoloClient` and `AddQwenVlClient` HttpClient registrations:
  - Retry on `HttpRequestException` and transient HTTP errors (5xx, 408)
  - 3 retries with exponential backoff (2s, 4s, 8s) + jitter
  - Log each retry attempt using `ILogger`
- Add a **circuit breaker policy** chained after retry:
  - Break after 5 consecutive failures
  - Break duration: 30 seconds
  - Log circuit state transitions (open/half-open/closed)
- Use `Polly.Extensions.Http.HttpPolicyExtensions.HandleTransientHttpError()` as the base selector
- Read retry count and circuit breaker settings from `YoloOptions` and `QwenVlOptions`

### 2. Modify `src/VisionService/Configuration/YoloOptions.cs`
Add properties:
- `MaxRetries` (int, default 3) — already exists, verify it's used
- `CircuitBreakerThreshold` (int, default 5)
- `CircuitBreakerDurationSeconds` (int, default 30)

### 3. Modify `src/VisionService/Configuration/QwenVlOptions.cs`
Add same circuit breaker properties.

### 4. Update `src/VisionService/appsettings.json`
Add circuit breaker defaults to `Yolo` and `QwenVl` sections.

### 5. Add tests in `src/VisionService.Tests/Clients/`
- Test that retry policy retries on transient errors
- Test that circuit breaker opens after threshold failures
- Use `NSubstitute` or test `DelegatingHandler` to simulate failures

## Acceptance Criteria
- [ ] HTTP clients retry transient failures with exponential backoff
- [ ] Circuit breaker opens after consecutive failures and resets after duration
- [ ] Retry and circuit breaker settings are configurable via `appsettings.json`
- [ ] At least 2 new tests (happy retry + circuit breaker open)
- [ ] All existing tests pass

## Files to Modify
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
- `src/VisionService/Configuration/YoloOptions.cs`
- `src/VisionService/Configuration/QwenVlOptions.cs`
- `src/VisionService/appsettings.json`

## Files to Create
- `src/VisionService.Tests/Clients/PollyResilienceTests.cs` (new)
