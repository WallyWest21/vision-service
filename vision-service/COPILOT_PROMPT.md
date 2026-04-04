# Copilot Coding Agent Prompt

> **Copy everything below this line and paste it into the Copilot coding agent chat in Visual Studio.**

---

Read the implementation plan in `docs/service-plan.md` and the coding conventions in `.github/copilot-instructions.md`. Implement the entire Vision Microservice described in the plan.

**Break the work into 14 separate pull requests, one per section in the PR Breakdown.** Implement them in exact order: PR 1 through PR 14.

For each PR:
1. Create a feature branch named `feature/pr-{number}-{short-description}` (e.g., `feature/pr-01-scaffold`, `feature/pr-02-configuration`)
2. Write a clear PR title matching the plan section name and a description listing all files added/changed
3. Implement ALL files described in that section — no placeholders, no TODO comments, no stub implementations
4. Include complete, passing xUnit tests as specified in the plan for that PR
5. Ensure `dotnet build` and `dotnet test` pass for the full solution
6. Base each PR on `main`

**Critical rules:**
- Do NOT stop or ask for confirmation between PRs — create all 14 PRs autonomously
- Every PR must compile and pass all tests independently when merged to main
- Follow the coding conventions in `.github/copilot-instructions.md` exactly
- Use the existing `Program.cs`, `appsettings.json`, project files, and Docker files as your starting point — extend them, don't recreate them
- The YOLO backend is a FastAPI server at `http://yolo-api:7860` with endpoints: `/detect`, `/segment`, `/pose`, `/classify`
- The Qwen-VL backend is a vLLM OpenAI-compatible server at `http://qwen-vl:8000` using `/v1/chat/completions` with image_url content type
- For integration tests, use `WebApplicationFactory<Program>` with mocked HTTP backends
- Production Docker image must build and pass healthcheck

Start now with PR 1 — Project Scaffold & Docker Infrastructure.
