# Deployment Guide

This guide covers deploying the Vision Service stack in local, server, and Kubernetes environments.

---

## Table of Contents

1. [Docker Compose — Local](#docker-compose--local)
2. [Docker Compose — Server / Production](#docker-compose--server--production)
3. [Kubernetes Deployment](#kubernetes-deployment)
4. [Environment Variables Reference](#environment-variables-reference)
5. [GPU Setup](#gpu-setup)
6. [TLS Termination](#tls-termination)
7. [Monitoring](#monitoring)
8. [Backup and Retention](#backup-and-retention)

---

## Docker Compose — Local

### Prerequisites

- Docker Desktop (with [WSL 2 backend](https://docs.docker.com/desktop/wsl/) on Windows)
- NVIDIA GPU + drivers + [`nvidia-container-toolkit`](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html) for GPU inference (optional — see [CPU-only](#cpu-only) below)

### Start the full stack

```bash
git clone https://github.com/WallyWest21/vision-service.git
cd vision-service/vision-service

docker compose up -d
```

Services started:

| Service | URL |
|---------|-----|
| VisionService | http://localhost:5100 |
| Swagger UI | http://localhost:5100/swagger |
| yolo-api | http://localhost:7860 |
| qwen-vl | http://localhost:8000 |
| Prometheus metrics | http://localhost:5100/metrics |

### CPU-only

If no GPU is available, override the compose file to remove GPU device reservations:

```bash
docker compose -f docker-compose.yml -f docker-compose.cpu.yml up -d
```

> **Note:** Inference will be significantly slower without a GPU.

### Development override

The `docker-compose.dev.yml` file mounts the source tree for live reload and exposes debug ports:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

### Useful commands

```bash
# View logs for all services
docker compose logs -f

# View logs for a single service
docker compose logs -f vision-service

# Check health of all containers
docker compose ps

# Stop and remove containers
docker compose down

# Stop, remove containers, and delete volumes
docker compose down -v
```

---

## Docker Compose — Server / Production

Use `docker-compose.prod.yml` which:

- Removes build context (uses pre-built images)
- Enables authentication (`Auth__Enabled=true`)
- Sets resource limits and restart policies

### Steps

1. **Build and push images** (or use pre-built images from GHCR):

   ```bash
   docker build -f docker/Dockerfile -t ghcr.io/wallywest21/vision-service:latest .
   docker push ghcr.io/wallywest21/vision-service:latest
   ```

2. **Copy files to the server:**

   ```bash
   scp docker-compose.prod.yml user@server:/opt/vision-service/docker-compose.yml
   ```

3. **Create a `.env` file** on the server (never commit this):

   ```env
   AUTH__APIKEYS__0__KEY=your-secret-api-key-here
   AUTH__APIKEYS__0__NAME=default
   AUTH__APIKEYS__0__SCOPES__0=detect
   AUTH__APIKEYS__0__SCOPES__1=analyze
   AUTH__APIKEYS__0__SCOPES__2=admin
   AUTH__APIKEYS__0__SCOPES__3=stream
   ```

4. **Start:**

   ```bash
   cd /opt/vision-service
   docker compose up -d
   ```

### Resource limits (prod defaults)

| Service | CPU limit | Memory limit |
|---------|-----------|--------------|
| vision-service | 2.0 | 1 GB |
| yolo-api | 2.0 | 4 GB |
| qwen-vl | 4.0 | 16 GB |

Adjust limits in `docker-compose.prod.yml` under `deploy.resources.limits` as needed.

---

## Kubernetes Deployment

Reference manifests for the `vision-system` namespace. Apply with:

```bash
kubectl apply -f k8s/
```

### Namespace

```yaml
# k8s/namespace.yml
apiVersion: v1
kind: Namespace
metadata:
  name: vision-system
```

### ConfigMap

```yaml
# k8s/vision-service/configmap.yml
apiVersion: v1
kind: ConfigMap
metadata:
  name: vision-service-config
  namespace: vision-system
data:
  Yolo__BaseUrl: "http://yolo-api:7860"
  QwenVl__BaseUrl: "http://qwen-vl:8000"
  QwenVl__ModelName: "Qwen/Qwen2.5-VL-7B-Instruct"
  RateLimit__RequestsPerMinute: "600"
  Cache__Enabled: "true"
  Cache__DefaultTtlSeconds: "300"
  Auth__Enabled: "true"
  ASPNETCORE_ENVIRONMENT: "Production"
```

### Secret (template — replace placeholders)

```yaml
# k8s/vision-service/secret.yml
apiVersion: v1
kind: Secret
metadata:
  name: vision-service-secret
  namespace: vision-system
type: Opaque
data:
  Auth__ApiKeys__0__Key: BASE64_ENCODED_API_KEY
  Auth__ApiKeys__0__Name: BASE64_ENCODED_KEY_NAME
```

Generate base64 values:

```bash
echo -n "my-secret-key" | base64
```

### Deployment

```yaml
# k8s/vision-service/deployment.yml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vision-service
  namespace: vision-system
  labels:
    app: vision-service
spec:
  replicas: 2
  selector:
    matchLabels:
      app: vision-service
  template:
    metadata:
      labels:
        app: vision-service
    spec:
      containers:
        - name: vision-service
          image: ghcr.io/wallywest21/vision-service:latest
          ports:
            - containerPort: 5100
          envFrom:
            - configMapRef:
                name: vision-service-config
            - secretRef:
                name: vision-service-secret
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "512Mi"
              cpu: "500m"
          livenessProbe:
            httpGet:
              path: /health/live
              port: 5100
            initialDelaySeconds: 10
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 5100
            initialDelaySeconds: 30
            periodSeconds: 15
          volumeMounts:
            - name: image-storage
              mountPath: /data/images
      volumes:
        - name: image-storage
          persistentVolumeClaim:
            claimName: vision-service-pvc
```

### Service (ClusterIP)

```yaml
# k8s/vision-service/service.yml
apiVersion: v1
kind: Service
metadata:
  name: vision-service
  namespace: vision-system
  labels:
    app: vision-service
spec:
  selector:
    app: vision-service
  ports:
    - port: 5100
      targetPort: 5100
```

### PersistentVolumeClaim

```yaml
# k8s/vision-service/pvc.yml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: vision-service-pvc
  namespace: vision-system
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
```

### HorizontalPodAutoscaler

```yaml
# k8s/vision-service/hpa.yml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: vision-service-hpa
  namespace: vision-system
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: vision-service
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
```

### Ingress (with TLS placeholder)

```yaml
# k8s/vision-service/ingress.yml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: vision-service-ingress
  namespace: vision-system
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-body-size: "25m"
spec:
  tls:
    - hosts:
        - vision.example.com
      secretName: vision-service-tls
  rules:
    - host: vision.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: vision-service
                port:
                  number: 5100
```

---

## Environment Variables Reference

All `appsettings.json` keys can be overridden using environment variables with `__` (double underscore) as the separator.

### Core service

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment (`Development`, `Staging`, `Production`) |
| `ASPNETCORE_URLS` | `http://+:5100` | Kestrel listen address |

### YOLO backend

| Variable | Default | Description |
|----------|---------|-------------|
| `Yolo__BaseUrl` | `http://yolo-api:7860` | YOLOv8 FastAPI base URL |
| `Yolo__TimeoutSeconds` | `30` | HTTP request timeout |
| `Yolo__MaxRetries` | `3` | Polly retry count |
| `Yolo__CircuitBreakerThreshold` | `5` | Failures before circuit opens |
| `Yolo__CircuitBreakerDurationSeconds` | `30` | Circuit open duration (seconds) |

### Qwen-VL backend

| Variable | Default | Description |
|----------|---------|-------------|
| `QwenVl__BaseUrl` | `http://qwen-vl:8000` | Qwen-VL vLLM base URL |
| `QwenVl__ModelName` | `Qwen/Qwen2.5-VL-7B-Instruct` | Model identifier |
| `QwenVl__MaxTokens` | `1024` | Maximum response tokens |
| `QwenVl__Temperature` | `0.7` | Sampling temperature (0.0–2.0) |
| `QwenVl__TimeoutSeconds` | `120` | HTTP request timeout |
| `QwenVl__MaxRetries` | `3` | Polly retry count |

### Storage

| Variable | Default | Description |
|----------|---------|-------------|
| `Storage__ImageStoragePath` | `/data/images` | Image storage root path |
| `Storage__RetentionDays` | `7` | Days before auto-cleanup |
| `Storage__MaxFileSizeMb` | `20` | Max upload file size in MB |

### Authentication

| Variable | Default | Description |
|----------|---------|-------------|
| `Auth__Enabled` | `false` | Enable API key enforcement |
| `Auth__ApiKeys__0__Key` | *(none)* | API key value |
| `Auth__ApiKeys__0__Name` | *(none)* | Display name |
| `Auth__ApiKeys__0__Scopes__0` | *(none)* | Scope (e.g., `detect`, `analyze`, `admin`, `stream`) |
| `Auth__ApiKeys__0__RequestsPerMinute` | `0` | Per-key rate limit (0 = use default) |

### Rate limiting

| Variable | Default | Description |
|----------|---------|-------------|
| `RateLimit__RequestsPerMinute` | `3000` | Default requests per minute per IP |
| `RateLimit__BurstSize` | `100` | Burst allowance |

### Caching

| Variable | Default | Description |
|----------|---------|-------------|
| `Cache__Enabled` | `true` | Enable in-memory response cache |
| `Cache__DefaultTtlSeconds` | `300` | Cache entry TTL |
| `Cache__MaxItems` | `1000` | Max cached entries |

### Performance

| Variable | Default | Description |
|----------|---------|-------------|
| `Performance__MinAiIntervalMs` | `500` | Min ms between WebSocket AI calls |
| `Performance__MaxWebSocketFrameBytes` | `5242880` | Max WebSocket frame size (5 MB) |
| `Performance__HealthCheckIntervalSeconds` | `30` | Backend health probe interval |
| `Performance__ImageCleanupIntervalHours` | `6` | Cleanup job interval |
| `Performance__MaxConcurrentAiRequests` | `0` | Max concurrent AI requests (0 = unlimited) |

### Observability

| Variable | Description |
|----------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint for traces/metrics (e.g., `http://otel-collector:4317`) |

---

## GPU Setup

Both the YOLO and Qwen-VL containers require an NVIDIA GPU for practical inference speeds.

### Host requirements

1. Install the [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html):

   ```bash
   # Ubuntu/Debian
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
   distribution=$(. /etc/os-release; echo "$ID$VERSION_ID")
   curl -sL https://nvidia.github.io/libnvidia-container/$distribution/libnvidia-container.list | \
     sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
     sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit
   sudo nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

2. Verify GPU is accessible from Docker:

   ```bash
   docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
   ```

### Docker Compose GPU configuration

GPU access is already configured in `docker-compose.yml`:

```yaml
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 1
          capabilities: [gpu]
```

### Kubernetes GPU setup

For Kubernetes, install the [NVIDIA device plugin](https://github.com/NVIDIA/k8s-device-plugin):

```bash
kubectl apply -f https://raw.githubusercontent.com/NVIDIA/k8s-device-plugin/v0.14.1/nvidia-device-plugin.yml
```

Then add GPU resource limits to yolo-api and qwen-vl deployments:

```yaml
resources:
  limits:
    nvidia.com/gpu: 1
```

---

## TLS Termination

The VisionService itself runs on plain HTTP (port 5100). TLS should be terminated at a reverse proxy layer.

### Nginx (Docker Compose)

Add an `nginx` service to your compose file:

```yaml
nginx:
  image: nginx:alpine
  ports:
    - "443:443"
    - "80:80"
  volumes:
    - ./nginx.conf:/etc/nginx/nginx.conf:ro
    - ./certs:/etc/nginx/certs:ro
  depends_on:
    - vision-service
```

Example `nginx.conf`:

```nginx
server {
    listen 443 ssl;
    server_name vision.example.com;

    ssl_certificate     /etc/nginx/certs/fullchain.pem;
    ssl_certificate_key /etc/nginx/certs/privkey.pem;

    # Increase proxy timeout for AI inference
    proxy_read_timeout 120s;
    proxy_send_timeout 120s;

    # Allow large image uploads (match Storage:MaxFileSizeMb + overhead)
    client_max_body_size 25m;

    location / {
        proxy_pass http://vision-service:5100;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket support
    location /ws/ {
        proxy_pass http://vision-service:5100;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

### Kubernetes — cert-manager + Let's Encrypt

1. Install cert-manager:

   ```bash
   kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.14.0/cert-manager.yaml
   ```

2. Create a `ClusterIssuer`:

   ```yaml
   apiVersion: cert-manager.io/v1
   kind: ClusterIssuer
   metadata:
     name: letsencrypt-prod
   spec:
     acme:
       server: https://acme-v02.api.letsencrypt.org/directory
       email: your-email@example.com
       privateKeySecretRef:
         name: letsencrypt-prod
       solvers:
         - http01:
             ingress:
               class: nginx
   ```

3. The Ingress manifest above already includes the `cert-manager.io/cluster-issuer` annotation, so TLS certificates are issued automatically.

---

## Monitoring

### Prometheus

Add a scrape job to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: vision-service
    scrape_interval: 15s
    static_configs:
      - targets: ["vision-service:5100"]
    metrics_path: /metrics
```

Key metrics exposed:

| Metric | Type | Description |
|--------|------|-------------|
| `http_requests_received_total` | Counter | Total HTTP requests by method, route, status code |
| `http_request_duration_seconds` | Histogram | Request duration distribution |
| `http_requests_in_progress` | Gauge | Currently active requests |
| `dotnet_gc_collections_total` | Counter | .NET GC collections by generation |
| `process_cpu_seconds_total` | Counter | Total process CPU time |
| `process_working_set_bytes` | Gauge | Working set memory |

### Grafana

Import the **ASP.NET Core** community dashboard (ID `10915`) or build a custom dashboard. Recommended panels:

- **Request rate** — `rate(http_requests_received_total[5m])`
- **Error rate** — `rate(http_requests_received_total{code=~"5.."}[5m])`
- **p99 latency** — `histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m]))`
- **In-progress requests** — `http_requests_in_progress`
- **Memory** — `process_working_set_bytes`

### Jaeger / OpenTelemetry

Set the OTLP endpoint environment variable to export traces:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
```

Traces include:

- Incoming HTTP request spans (with correlation ID propagation via `X-Correlation-Id`)
- Outbound calls to yolo-api and qwen-vl
- Cache hit/miss events

To run Jaeger locally alongside the stack, add to `docker-compose.override.yml`:

```yaml
jaeger:
  image: jaegertracing/all-in-one:latest
  ports:
    - "16686:16686"   # Jaeger UI
    - "4317:4317"     # OTLP gRPC
  environment:
    - COLLECTOR_OTLP_ENABLED=true
```

Then open **[http://localhost:16686](http://localhost:16686)** to explore traces.

---

## Backup and Retention

### Image storage volume

Uploaded images are stored at `Storage:ImageStoragePath` (default `/data/images`), which is mounted as a Docker named volume (`image-storage`) or a Kubernetes PVC.

**Docker Compose backup:**

```bash
# Create a tar archive of the volume contents
docker run --rm \
  -v vision-service_image-storage:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/images-$(date +%Y%m%d).tar.gz -C /data .
```

**Kubernetes backup with Velero:**

```bash
velero backup create vision-images \
  --include-namespaces vision-system \
  --selector app=vision-service
```

### Automatic cleanup

The `ImageCleanupJob` background service runs every `Performance:ImageCleanupIntervalHours` hours (default: 6) and deletes images older than `Storage:RetentionDays` days (default: 7).

To tune cleanup behaviour, update the settings at runtime via the admin API:

```bash
curl -X PUT http://localhost:5100/api/v1/admin/settings \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{
    "Storage": { "RetentionDays": 3 },
    "Performance": { "ImageCleanupIntervalHours": 2 }
  }'
```

Changes take effect immediately without restarting the service.
