## Context
Endpoints accept file uploads without verifying magic bytes, and confidence/query parameters have minimal validation. Production needs defense-in-depth input validation.

## Requirements

### 1. Create `src/VisionService/Services/FileValidationService.cs`
- Validate file extensions against `StorageOptions.AllowedExtensions`
- Validate file magic bytes (JPEG: `FF D8 FF`, PNG: `89 50 4E 47`, WebP: `52 49 46 46`, BMP: `42 4D`, GIF: `47 49 46 38`)
- Validate file size against `StorageOptions.MaxFileSizeMb`
- Return typed validation result (not exceptions)

### 2. Create `src/VisionService/Services/IFileValidationService.cs` - interface

### 3. Modify all endpoints that accept `IFormFile` to validate files before processing
- `YoloEndpoints` (detect, segment, classify, pose, batch)
- `QwenVlEndpoints` (ask, caption, ocr, analyze, compare)
- `PipelineEndpoints` (detect-and-describe, safety-check, inventory, scene)
- `PlaygroundEndpoints` (playground)
- Return `Results.Problem("...", statusCode: 400)` on validation failure

### 4. Modify `src/VisionService/Program.cs`
Configure Kestrel request size limits:
`builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 20 * 1024 * 1024);`

### 5. Register `IFileValidationService` in `ServiceCollectionExtensions.cs`

### 6. Add tests
- Valid JPEG passes validation
- Invalid extension is rejected
- Oversized file is rejected
- Wrong magic bytes for declared extension is rejected

## Acceptance Criteria
- [ ] Uploads with invalid extensions return 400
- [ ] Uploads with mismatched magic bytes return 400
- [ ] Uploads exceeding size limit return 400
- [ ] At least 4 new validation tests
- [ ] All existing tests pass

## Files to Create
- `src/VisionService/Services/IFileValidationService.cs` (new)
- `src/VisionService/Services/FileValidationService.cs` (new)
- `src/VisionService.Tests/Services/FileValidationServiceTests.cs` (new)

## Files to Modify
- `src/VisionService/Extensions/ServiceCollectionExtensions.cs`
- `src/VisionService/Endpoints/YoloEndpoints.cs`
- `src/VisionService/Endpoints/QwenVlEndpoints.cs`
- `src/VisionService/Endpoints/PipelineEndpoints.cs`
- `src/VisionService/Endpoints/PlaygroundEndpoints.cs`
- `src/VisionService/Program.cs`
