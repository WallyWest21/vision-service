## Context
The repository has no main README or deployment documentation. Contributors and operators need clear instructions.

## Requirements

### 1. Create `README.md` at repo root
- Project description: Camera vision microservice orchestrating YOLOv8 + Qwen-VL
- Architecture diagram (Mermaid) showing: MAUI Client -> VisionService -> YOLO API / Qwen-VL
- Quick start with `docker compose up`
- API overview table: endpoint, method, description
- Configuration reference: all options sections with defaults
- Link to Swagger UI at `/swagger`
- Development setup instructions (prerequisites, build, test)
- License section

### 2. Create `docs/deployment-guide.md`
- Docker Compose deployment (local + server)
- Kubernetes deployment (reference manifests from Issue 12)
- Environment variables reference
- GPU setup for YOLO and Qwen-VL containers
- TLS termination recommendations (reverse proxy)
- Monitoring: Prometheus scrape config, Grafana dashboard tips, Jaeger trace viewing
- Backup and retention: image storage volume, cleanup job configuration

### 3. Create `docs/api-reference.md`
- All endpoints grouped by tag (YOLO, Qwen-VL, Pipeline, Admin, Playground, WebSocket)
- Request/response examples with `curl`
- Authentication header usage
- WebSocket connection example with `websocat`

## Dependencies
> Should be created **last** so it documents everything from all other issues.

## Acceptance Criteria
- [ ] `README.md` exists at repo root with all sections
- [ ] `docs/deployment-guide.md` covers Docker + K8s deployment
- [ ] `docs/api-reference.md` has examples for every endpoint
- [ ] No broken markdown links
- [ ] All existing tests pass

## Files to Create
- `README.md` (new)
- `docs/deployment-guide.md` (new)
- `docs/api-reference.md` (new)
