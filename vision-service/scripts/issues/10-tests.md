## Context
Current test coverage is partial. The following endpoint/service groups lack adequate testing: Qwen-VL endpoints, Pipeline endpoints, Playground endpoints, WebSocket endpoints, Admin endpoints (partial), and client resilience behavior.

## Requirements

### 1. Expand `src/VisionService.Tests/Endpoints/QwenVlEndpointTests.cs`
- Test `POST /api/v1/ask` with valid image -> 200
- Test `POST /api/v1/caption` with valid image -> 200
- Test `POST /api/v1/ocr` with valid image -> 200
- Test when Qwen-VL backend is unavailable -> 503

### 2. Expand `src/VisionService.Tests/Endpoints/PipelineEndpointTests.cs`
- Test `POST /api/v1/pipeline/detect-and-describe` -> 200
- Test `POST /api/v1/pipeline/safety-check` -> 200
- Test backend failure -> 503

### 3. Create `src/VisionService.Tests/Endpoints/PlaygroundEndpointTests.cs`
- Test `POST /api/v1/playground` -> 200 with mocked clients
- Test backend failure -> 503

### 4. Expand `src/VisionService.Tests/Endpoints/AdminEndpointTests.cs`
- Test `GET /api/v1/admin/settings` -> 200 with expected shape
- Test `PUT /api/v1/admin/settings` -> 200 and values updated
- Test `POST /api/v1/admin/keys` with missing name -> 400

### 5. Create `src/VisionService.Tests/Clients/QwenVlClientTests.cs`
- Test `CaptionAsync` with successful HTTP response
- Test `AskAsync` with backend failure throws `HttpRequestException`

### 6. All test classes MUST follow conventions
- Test framework: xUnit
- Mocking: NSubstitute
- Assertions: FluentAssertions
- Naming: `MethodName_Scenario_ExpectedResult`
- At least one happy path + one failure path per endpoint

## Dependencies
> Best created after **Issues 3, 4, 7, 9** (so tests cover new features).

## Acceptance Criteria
- [ ] All listed test methods exist and pass
- [ ] Every endpoint has at least one happy path and one failure test
- [ ] `dotnet test` runs green with no skipped tests
- [ ] No `// TODO` comments in test files

## Files to Create
- `src/VisionService.Tests/Endpoints/PlaygroundEndpointTests.cs` (new)
- `src/VisionService.Tests/Clients/QwenVlClientTests.cs` (new)

## Files to Modify
- `src/VisionService.Tests/Endpoints/QwenVlEndpointTests.cs`
- `src/VisionService.Tests/Endpoints/PipelineEndpointTests.cs`
- `src/VisionService.Tests/Endpoints/AdminEndpointTests.cs`
