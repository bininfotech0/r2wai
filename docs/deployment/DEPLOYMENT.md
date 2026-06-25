# R2WAI Deployment Guide

## Prerequisites

- Kubernetes cluster v1.28+ (AKS, EKS, GKE, or on-prem)
- `kubectl` v1.28+ configured with cluster context
- Helm v3+ (optional, for package management)
- Container registry access (GHCR, Docker Hub, or ACR)
- Domain names configured with DNS:
  - `app.r2wai.com` — production
  - `staging.r2wai.com` — staging
- TLS certificate for `*.r2wai.com`
- Slack webhook URL for deployment notifications

## Deployment Architecture

```
User ──► Cloudflare/Azure Front Door ──► AKS Cluster
                                              │
                                    ┌─────────┴─────────┐
                                    │  Ingress Controller│
                                    │  (nginx-ingress)  │
                                    └─────────┬─────────┘
                                              │
                         ┌────────────────────┼────────────────────┐
                         │                    │                    │
                   ┌─────┴─────┐       ┌──────┴──────┐      ┌────┴────┐
                   │  r2wai-api│       │ r2wai-web   │      │ r2wai-  │
                   │  (scaled) │       │  (scaled)   │      │  config │
                   └─────┬─────┘       │  (scaled)   │      └─────────┘
                         │             └─────────────┘
                         │
            ┌────────────┼────────────┬────────────┐
            │            │            │            │
       ┌────┴────┐ ┌─────┴─────┐ ┌───┴────┐ ┌───┴────┐
       │Postgres │ │  Redis    │ │ Qdrant │ │ MinIO  │
       │(HA)     │ │  (Cluster)│ │(HA)    │ │        │
       └─────────┘ └───────────┘ └────────┘ └────────┘
```

## Docker Deployment

### Build images

```bash
# API
docker build -f docker/Dockerfile.api -t r2wai-api:latest .

# Blazor web app
docker build -f docker/Dockerfile.web -t r2wai-web:latest .
```

### Run with docker-compose

```bash
# Full stack
docker compose -f docker/docker-compose.yml up -d

# Verify
curl http://localhost:5000/health
```

## Kubernetes Deployment

### 1. Configure environment

```bash
# Set target cluster context
kubectl config use-context <cluster-name>

# Verify connectivity
kubectl cluster-info
```

### 2. Create namespace and secrets

```bash
kubectl apply -f k8s/namespace.yaml
```

Create the required secrets:

```bash
# TLS certificate
kubectl create secret tls r2wai-tls \
  --namespace r2wai \
  --cert=path/to/tls.crt \
  --key=path/to/tls.key

# Database & service secrets
kubectl apply -f k8s/secret.yaml
```

Edit `k8s/secret.yaml` with production values before applying.

### 3. Deploy infrastructure

```bash
kubectl apply -k k8s/ --dry-run=client
kubectl apply -k k8s/
```

### 4. Verify deployment

```bash
# Watch pods come online
kubectl get pods -n r2wai -w

# Check deployments
kubectl rollout status deployment/r2wai-api -n r2wai
kubectl rollout status deployment/r2wai-web -n r2wai

# Verify services
kubectl get svc -n r2wai

# Check ingress
kubectl get ingress -n r2wai
```

### 5. Verify health endpoints

```bash
# API health
curl https://app.r2wai.com/health

# Web app
curl https://app.r2wai.com/
```

## CI/CD Pipeline

The CD pipeline in `.github/workflows/cd.yml` automates:

1. **Build & Push** — Docker images built with BuildKit cache, pushed to GHCR
2. **Staging Deploy** — Images deployed to staging namespace with rollout verification
3. **Production Promote** — Same images promoted to production after staging verification
4. **Health Check** — HTTP health endpoint polled after deployment
5. **Slack Notification** — Success/failure notification sent to Slack

### Required Secrets

| Secret | Description |
|---|---|
| `KUBECONFIG_STAGING` | Base64-encoded kubeconfig for staging cluster |
| `KUBECONFIG_PRODUCTION` | Base64-encoded kubeconfig for production cluster |
| `SLACK_WEBHOOK_URL` | Slack incoming webhook URL for notifications |
| `GITHUB_TOKEN` | Auto-provided by GitHub Actions |

## Configuration

### ConfigMap

Non-sensitive configuration is stored in `k8s/configmap.yaml`:

| Key | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment |
| `ConnectionStrings__Redis` | Redis connection string |
| `Storage__Provider` | Storage backend (`MinIO`) |
| `Storage__MinIO__Endpoint` | MinIO service endpoint |
| `Qdrant__Host` | Qdrant service host |
| `POSTGRES_DB` | Database name |

### Secrets

Sensitive configuration is stored in `k8s/secret.yaml` and Kubernetes Secrets:

| Key | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Storage__MinIO__AccessKey` | MinIO access key |
| `Storage__MinIO__SecretKey` | MinIO secret key |
| `Jwt__Key` | JWT signing key (256-bit minimum) |
| `AI__OpenAI__ApiKey` | OpenAI API key |

## Monitoring

### Health Endpoints

| Endpoint | Description |
|---|---|
| `/health` | Basic liveness check |
| `/health/ready` | Readiness (DB, Redis, Qdrant connectivity) |
| `/health/startup` | Startup probe |

### Logging

- Structured JSON logs via Serilog
- Log shipping: File → Fluentd → Elasticsearch (configure via Helm values)
- Centralized log viewer: Kibana or Grafana Loki

### Metrics

- Prometheus metrics at `/metrics` (if enabled)
- Pre-configured Grafana dashboards for:
  - Request rate, latency, error rate (RED metrics)
  - .NET GC, thread pool, JIT stats
  - AI model token usage and latency
  - Database connection pooling
  - Redis cache hit ratio

### Alerts (via Prometheus + Alertmanager)

| Alert | Threshold |
|---|---|
| `HighErrorRate` | HTTP 5xx rate > 1% over 5m |
| `HighLatency` | p99 latency > 2s over 5m |
| `PodCrashLooping` | Pod restart > 3 in 10m |
| `HighMemoryUsage` | Memory > 85% of limit |
| `DiskSpaceLow` | Persistent volume usage > 80% |

## Scaling

### Horizontal Pod Autoscaling

The API is configured with HPA in `k8s/hpa-api.yaml`:

- Min replicas: 2
- Max replicas: 10
- CPU threshold: 70% average utilization
- Memory threshold: 80% average utilization

### Manual scaling

```bash
kubectl scale deployment/r2wai-api -n r2wai --replicas=5
```

### Database scaling

PostgreSQL can be scaled vertically via the `postgres-deployment.yaml` resource limits.

## Backup & Recovery

### PostgreSQL

```bash
# Backup
kubectl exec -n r2wai deployment/postgres -- pg_dump -U r2wai r2wai > backup.sql

# Restore
cat backup.sql | kubectl exec -i -n r2wai deployment/postgres -- psql -U r2wai r2wai
```

### MinIO

Use `mc` client or configure a CronJob:

```bash
kubectl exec -n r2wai deployment/minio -- mc mirror --watch /data s3://backup-bucket/
```

### Qdrant

Snapshot API:

```bash
curl -X POST 'http://qdrant:6333/collections/{name}/snapshots'
```

### Velero (recommended)

Install Velero for cluster-level backup of all resources and persistent volumes.

## Security Checklist

- [ ] JWT signing key rotated and stored in Kubernetes Secret (not in git)
- [ ] TLS certificate valid and auto-renewing (cert-manager)
- [ ] Network policies restricting pod-to-pod traffic
- [ ] Pod security context: `runAsNonRoot: true`
- [ ] Container image scanning via Trivy in CI pipeline
- [ ] RBAC roles restricted to least privilege
- [ ] Database connection encrypted (TLS)
- [ ] API rate limiting configured (ingress annotation: `limit-rps: 100`)
- [ ] Audit logging enabled (all mutation operations logged to `AuditLogs` table)
- [ ] Secrets never logged (Serilog destructuring excludes secret fields)
- [ ] Regular vulnerability scanning scheduled
- [ ] Slack alerts on deployment failures

## Rollback

### Via kubectl

```bash
# Rollback to previous revision
kubectl rollout undo deployment/r2wai-api -n r2wai

# Rollback to specific revision
kubectl rollout undo deployment/r2wai-api -n r2wai --to-revision=3

# Check revision history
kubectl rollout history deployment/r2wai-api -n r2wai
```

### Via GitHub Actions

Re-run a previous workflow run that deployed the desired version, or trigger a manual deployment with a specific image tag using the `workflow_dispatch` event.
