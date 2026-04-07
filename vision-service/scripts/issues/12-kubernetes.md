## Context
For production deployment beyond Docker Compose, the service needs Kubernetes manifests with proper resource limits, probes, and scaling configuration.

## Requirements

### 1. Create `k8s/namespace.yml` - `vision-system` namespace

### 2. Create `k8s/vision-service/deployment.yml`
- 2 replicas (HPA scales 2-10)
- Container: `ghcr.io/wallywest21/vision-service:latest`
- Resource requests: 256Mi memory, 250m CPU
- Resource limits: 512Mi memory, 500m CPU
- Liveness probe: `GET /health/live` every 10s
- Readiness probe: `GET /health/ready` every 15s with 30s initial delay
- Environment variables from ConfigMap and Secret
- Volume mount for image storage (PVC)

### 3. Create `k8s/vision-service/service.yml` - ClusterIP on port 5100

### 4. Create `k8s/vision-service/configmap.yml` - non-secret config (BaseUrls, rate limits, cache)

### 5. Create `k8s/vision-service/secret.yml` - template with BASE64_ENCODED placeholders

### 6. Create `k8s/vision-service/hpa.yml` - HorizontalPodAutoscaler targeting 70% CPU

### 7. Create `k8s/vision-service/pvc.yml` - PersistentVolumeClaim 10Gi for image storage

### 8. Create `k8s/vision-service/ingress.yml` - Ingress with TLS annotation placeholders

## Dependencies
> Depends on **Issues 1, 4, 8** (Dockerfile, health check paths, graceful shutdown).

## Acceptance Criteria
- [ ] `kubectl apply -f k8s/ --dry-run=client` succeeds
- [ ] All manifests use consistent labels (`app: vision-service`)
- [ ] Probes reference correct health check paths
- [ ] Secret template contains only placeholders (no real values)
- [ ] All existing tests pass

## Files to Create
- `k8s/namespace.yml` (new)
- `k8s/vision-service/deployment.yml` (new)
- `k8s/vision-service/service.yml` (new)
- `k8s/vision-service/configmap.yml` (new)
- `k8s/vision-service/secret.yml` (new)
- `k8s/vision-service/hpa.yml` (new)
- `k8s/vision-service/pvc.yml` (new)
- `k8s/vision-service/ingress.yml` (new)
