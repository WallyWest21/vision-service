## Context
`VisionService.csproj` already references `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, and `OpenTelemetry.Instrumentation.Http`, but none are configured in `Program.cs`. Production needs distributed tracing across the service and its HTTP calls to YOLO/Qwen-VL backends.

## Requirements

### 1. Modify `src/VisionService/Program.cs`
- Add OpenTelemetry tracing configuration after `builder.Services.AddVisionServices(...)`:
  - `AddAspNetCoreInstrumentation()`
  - `AddHttpClientInstrumentation()`
  - `AddOtlpExporter()` - configurable via env `OTEL_EXPORTER_OTLP_ENDPOINT`
- Make the OTLP endpoint configurable (default: `http://localhost:4317`)
- Conditionally add console exporter when `ASPNETCORE_ENVIRONMENT=Development`

### 2. Add `OpenTelemetry.Exporter.OpenTelemetryProtocol` NuGet package
The OTLP exporter is not yet referenced in the csproj.

### 3. Create `src/VisionService/Diagnostics/VisionActivitySource.cs`
- Static `ActivitySource` named `"VisionService"`
- Add manual spans in `YoloClient.DetectAsync`, `QwenVlClient.CaptionAsync`, and `PipelineEndpoints.DetectAndDescribeAsync`

### 4. Update `docker-compose.yml` (if merged from Issue 1)
- Add optional `jaeger` service: `jaegertracing/all-in-one:latest`, ports `16686:16686` (UI) and `4317:4317` (OTLP gRPC)
- Set `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317` on `vision-service`

## Dependencies
> Uses `docker-compose.yml` from **Issue 1** (can still merge independently).

## Acceptance Criteria
- [ ] ASP.NET Core requests create trace spans
- [ ] HTTP client calls to YOLO/Qwen-VL create child spans
- [ ] Custom activity spans exist for key AI operations
- [ ] OTLP endpoint is configurable via environment
- [ ] All existing tests pass

## Files to Create
- `src/VisionService/Diagnostics/VisionActivitySource.cs` (new)

## Files to Modify
- `src/VisionService/VisionService.csproj`
- `src/VisionService/Program.cs`
- `src/VisionService/Clients/YoloClient.cs`
- `src/VisionService/Clients/QwenVlClient.cs`
- `src/VisionService/Endpoints/PipelineEndpoints.cs`
- `docker-compose.yml` (if exists)
