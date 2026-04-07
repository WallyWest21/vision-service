#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates the missing Issue 8 (Graceful Shutdown) that failed due to rate limits.
    Run this after the GitHub GraphQL rate limit resets.
.EXAMPLE
    powershell.exe -ExecutionPolicy Bypass -File scripts\create-issue8.ps1
#>
$ErrorActionPreference = "Stop"

$ghExe = Get-Command gh -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $ghExe) {
    $candidate = "C:\Program Files\GitHub CLI\gh.exe"
    if (Test-Path $candidate) { $ghExe = $candidate }
    else { Write-Error "GitHub CLI (gh) not found."; exit 1 }
}

# Check rate limit first
Write-Host "Checking rate limit..." -ForegroundColor DarkGray
$rateJson = & $ghExe api "rate_limit" 2>&1 | ConvertFrom-Json
$remaining = $rateJson.resources.graphql.remaining
$resetUnix = $rateJson.resources.graphql.reset
$resetTime = [DateTimeOffset]::FromUnixTimeSeconds($resetUnix).ToLocalTime().ToString("HH:mm:ss")

if ($remaining -lt 5) {
    Write-Host "GraphQL rate limit: $remaining remaining. Resets at $resetTime." -ForegroundColor Red
    Write-Host "Wait and try again after $resetTime." -ForegroundColor Yellow
    exit 1
}

Write-Host "Rate limit OK ($remaining remaining). Creating Issue 8..." -ForegroundColor Green

$bodyFile = Join-Path $PSScriptRoot "issue8-body.md"
& $ghExe issue create --repo WallyWest21/vision-service `
    --title "feat: Add graceful shutdown handling and Kubernetes-compatible probes" `
    --body-file $bodyFile `
    --label "infrastructure"

Write-Host ""
Write-Host "Done! Listing all open issues:" -ForegroundColor Cyan
& $ghExe issue list --repo WallyWest21/vision-service --limit 20 --state open
