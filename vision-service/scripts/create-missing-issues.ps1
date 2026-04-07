#!/usr/bin/env pwsh
# Creates the 9 missing issues using --body-file (avoids quoting problems).
# Issues 16 (Docker), 17 (CI/CD), and 18 (Graceful Shutdown) already exist.
$ErrorActionPreference = "Continue"

$ghExe = Get-Command gh -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $ghExe) {
    $candidate = "C:\Program Files\GitHub CLI\gh.exe"
    if (Test-Path $candidate) { $ghExe = $candidate }
    else { Write-Error "gh not found"; exit 1 }
}

$repo = "WallyWest21/vision-service"
$scriptDir = $PSScriptRoot
$issuesDir = Join-Path $scriptDir "issues"

$issues = @(
    @{ Title = "feat: Replace custom /health endpoint with ASP.NET Core Health Checks including YOLO and Qwen-VL probes"; Label = "observability"; File = "04-healthchecks.md" }
    @{ Title = "feat: Wire up OpenTelemetry distributed tracing for ASP.NET Core and HTTP clients"; Label = "observability"; File = "05-opentelemetry.md" }
    @{ Title = "feat: Add CORS policy, appsettings.Production.json, and environment variable overrides"; Label = "configuration"; File = "06-cors-config.md" }
    @{ Title = "feat: Add input validation, file type verification, and request size limits"; Label = "security"; File = "07-validation.md" }
    @{ Title = "feat: Add API key authentication and connection limits to WebSocket endpoint"; Label = "security"; File = "09-websocket-auth.md" }
    @{ Title = "test: Add missing unit and integration tests for full coverage"; Label = "testing"; File = "10-tests.md" }
    @{ Title = "docs: Add comprehensive README, API reference, and deployment guide"; Label = "documentation"; File = "11-docs.md" }
    @{ Title = "feat: Add Kubernetes deployment manifests for production deployment"; Label = "infrastructure"; File = "12-kubernetes.md" }
)

$created = 0
$total = $issues.Count
foreach ($issue in $issues) {
    $created++
    $bodyFile = Join-Path $issuesDir $issue.File
    Write-Host "[$created/$total] $($issue.Title)" -ForegroundColor Yellow
    & $ghExe issue create --repo $repo --title $issue.Title --body-file $bodyFile --label $issue.Label 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED - check rate limit or auth" -ForegroundColor Red
    }
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "Done. Listing all open issues:" -ForegroundColor Green
& $ghExe issue list --repo $repo --limit 20 --state open 2>&1
