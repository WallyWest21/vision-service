# Copilot Coding Agent Instructions

## Project Context
This is a .NET 8 camera vision microservice that orchestrates YOLOv8 and Qwen-VL AI backends running in Docker containers. The implementation plan is in `vision-service/docs/service-plan.md`.

## Code Conventions

### Architecture
- Use **Minimal API** pattern with endpoint groups (not controllers)
- Follow **Clean Architecture**: Clients → Services → Endpoints
- All dependencies registered via extension methods in `ServiceCollectionExtensions.cs`
- Use `IOptions<T>` pattern for all configuration

### Naming
- Interfaces prefixed with `I` (e.g., `IYoloClient`)
- Async methods suffixed with `Async`
- Configuration classes suffixed with `Options`
- Test classes suffixed with `Tests`, test methods use `MethodName_Scenario_ExpectedResult`

### Error Handling
- Use `Results.Problem()` for error responses (RFC 7807 ProblemDetails)
- Never throw raw exceptions from endpoints — always catch and map
- Log exceptions with Serilog structured logging

### Testing
- Every PR MUST include tests that pass independently
- Use **xUnit** as test framework
- Use **NSubstitute** for mocking
- Use **FluentAssertions** for assertions
- Integration tests use test fixture with `WebApplicationFactory<Program>`
- Include at least one happy path and one failure path per endpoint
- Test projects reference: `xunit 2.9+`, `FluentAssertions 7+`, `NSubstitute 5+`, `Microsoft.AspNetCore.Mvc.Testing`
- No stubs or TODOs — implement fully

### Docker
- .NET service runs on port 5100
- YOLO backend: `http://yolo-api:7860`
- Qwen-VL backend: `http://qwen-vl:8000` (OpenAI-compatible `/v1/chat/completions`)
- Use Docker service names for inter-container communication

### Dependencies to Use
- `Polly` v8 for resilience (retry, circuit breaker)
- `Serilog.AspNetCore` for logging
- `prometheus-net.AspNetCore` for metrics
- `Swashbuckle.AspNetCore` for OpenAPI
- `System.IO.Hashing` for content hashing

### PR Standards
- Each PR must compile independently
- Each PR must pass `dotnet test` with all tests green
- PR description must list what was added and how to test it
- Do not introduce `// TODO` comments — implement fully
- Include XML documentation on all public types and methods

## File Structure
```
vision-service/
  src/
    VisionService/
      Clients/          # HTTP clients for AI backends
      Configuration/    # Options classes
      Endpoints/        # Minimal API endpoint groups
      Events/           # Event bus and event types
      Jobs/             # Background hosted services
      Middleware/       # Custom middleware
      Models/           # DTOs and domain models
      Services/         # Business logic services
      Program.cs        # Entry point
    VisionService.Tests/
      Clients/          # Client unit tests
      Endpoints/        # Integration tests
      Services/         # Service unit tests
  docker/
    Dockerfile          # Multi-stage .NET build
  docs/
    service-plan.md     # Implementation plan
```
