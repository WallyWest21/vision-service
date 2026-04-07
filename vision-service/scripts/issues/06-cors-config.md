## Context
No CORS policy is configured (the MAUI client makes HTTP calls), and there is no production-specific configuration file. Secrets are hardcoded in `appsettings.json`.

## Requirements

### 1. Modify `src/VisionService/Program.cs`
- Add CORS service with a named policy `"VisionCors"`:
  - Allow origins from a configurable list (`Cors:AllowedOrigins` in appsettings)
  - Allow any header and method
  - Expose `X-Correlation-Id` header
- Apply CORS middleware after `UseResponseCompression` and before custom middleware

### 2. Create `src/VisionService/Configuration/CorsOptions.cs`
- `AllowedOrigins` (string[], default `["*"]`)
- Register in `ServiceCollectionExtensions.cs`

### 3. Create `src/VisionService/appsettings.Production.json`
- Serilog minimum level: `Warning` for default, `Error` for Microsoft.AspNetCore
- `Auth.Enabled`: `true`
- `RateLimit.RequestsPerMinute`: `600`
- `RateLimit.BurstSize`: `50`
- `Cache.DefaultTtlSeconds`: `600`
- `Cors.AllowedOrigins`: `[]` (must be explicitly set in deployment)

### 4. Create `docs/environment-variables.md`
Document all environment variable overrides:
- `Yolo__BaseUrl`, `QwenVl__BaseUrl`, `Auth__ApiKeys__0__Key`, `Storage__ImageStoragePath`
- `ASPNETCORE_ENVIRONMENT`, `OTEL_EXPORTER_OTLP_ENDPOINT`

## Acceptance Criteria
- [ ] CORS headers present on API responses when origin matches
- [ ] `appsettings.Production.json` overrides appropriate defaults
- [ ] Environment variable override documentation exists
- [ ] All existing tests pass

## Files to Create
- `src/VisionService/Configuration/CorsOptions.cs` (new)
- `src/VisionService/appsettings.Production.json` (new)
- `docs/environment-variables.md` (new)

## Files to Modify
- `src/VisionService/Program.cs`
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
