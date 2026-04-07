# Environment Variable Overrides

All `appsettings.json` values can be overridden via environment variables using .NET's standard hierarchical override convention: nested keys are separated by `__` (double underscore).

## AI Backend URLs

| Environment Variable | appsettings key | Default | Description |
|---|---|---|---|
| `Yolo__BaseUrl` | `Yolo.BaseUrl` | `http://yolo-api:7860` | Base URL for the YOLOv8 detection/classification backend |
| `QwenVl__BaseUrl` | `QwenVl.BaseUrl` | `http://qwen-vl:8000` | Base URL for the Qwen-VL vision-language backend (OpenAI-compatible) |

## Authentication

| Environment Variable | appsettings key | Default | Description |
|---|---|---|---|
| `Auth__ApiKeys__0__Key` | `Auth.ApiKeys[0].Key` | *(none)* | The API key value for the first key entry |

## Storage

| Environment Variable | appsettings key | Default | Description |
|---|---|---|---|
| `Storage__ImageStoragePath` | `Storage.ImageStoragePath` | `/data/images` | Filesystem path where uploaded images are persisted |

## ASP.NET Core / Hosting

| Environment Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Sets the runtime environment. Common values: `Development`, `Staging`, `Production`. Controls which `appsettings.<Environment>.json` overlay is loaded. |

## OpenTelemetry

| Environment Variable | Description |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP gRPC/HTTP endpoint for exporting traces and metrics (e.g., `http://otel-collector:4317`). |

## Example: Docker Compose override

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - Yolo__BaseUrl=http://yolo-api:7860
  - QwenVl__BaseUrl=http://qwen-vl:8000
  - Auth__ApiKeys__0__Key=my-secret-key
  - Storage__ImageStoragePath=/mnt/images
  - OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```
