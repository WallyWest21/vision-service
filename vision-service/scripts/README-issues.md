# How to Create the Production-Readiness Issues

## Prerequisites
1. **GitHub CLI** (`gh`) — installed at `C:\Program Files\GitHub CLI\gh.exe`
2. **Authentication** — one-time browser login (see below)

## Step 1: Authenticate with GitHub

Open a **regular PowerShell window** (not VS Developer PowerShell) and run:

```powershell
& "C:\Program Files\GitHub CLI\gh.exe" auth login
```

Choose:
- **GitHub.com**
- **HTTPS**
- **Login with a web browser** (recommended)

Follow the browser prompts to authorize. This only needs to be done once.

## Step 2: Run the Script

The script automatically creates labels and all 12 issues. Open **any** PowerShell window:

```powershell
cd C:\Users\Bruce\source\repos\WallyWest21\vision-service\vision-service
.\scripts\create-github-issues.ps1
```

This creates all 12 issues in https://github.com/WallyWest21/vision-service/issues

## Step 4: Assign to Copilot Coding Agent

For each issue on GitHub:
1. Open the issue
2. Click **Assignees** in the sidebar
3. Type **copilot** and select it
4. Copilot will automatically create a pull request

### Recommended assignment order (respects dependencies):

**Wave 1** (no dependencies — assign immediately):
- Issue 1: Docker Infrastructure
- Issue 3: Polly Resilience
- Issue 4: Health Checks
- Issue 6: CORS & Production Config

**Wave 2** (after Wave 1 merges):
- Issue 2: CI/CD Pipeline (needs Issue 1)
- Issue 5: OpenTelemetry (needs Issue 1)
- Issue 7: Request Validation
- Issue 9: WebSocket Auth

**Wave 3** (after Wave 2 merges):
- Issue 8: Graceful Shutdown (needs Issue 4)
- Issue 10: Comprehensive Tests (needs Issues 3,4,7,9)

**Wave 4** (final):
- Issue 12: Kubernetes Manifests (needs Issues 1,4,8)
- Issue 11: README & Docs (last — documents everything)
