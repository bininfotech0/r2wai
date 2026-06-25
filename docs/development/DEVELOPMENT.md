# R2WAI Development Guide

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (v24+)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)
- IDE: [VS Code](https://code.visualstudio.com/) (recommended) or [JetBrains Rider](https://www.jetbrains.com/rider/)
  - VS Code extensions: `csharp`

## Quick Start

### 1. Clone and configure

```bash
git clone <repo-url> r2wai
cd r2wai
```

### 2. Start infrastructure services

```bash
docker compose -f docker/docker-compose.yml up -d postgres redis qdrant minio
```

This starts:
- **PostgreSQL 16** on `:5432` — primary database
- **Redis 7** on `:6379` — caching & SignalR backplane
- **Qdrant** on `:6333` — vector store
- **MinIO** on `:9000` (:9001 console) — object storage

### 3. Start the backend

```bash
cd src/R2WAI.Api
dotnet restore
dotnet run
```

The API starts on `http://localhost:5000` with Swagger at `/swagger`.

### 4. Start the web app

```bash
cd src/R2WAI.Web
dotnet run
```

The Blazor Server web app starts on `https://localhost:3000` and `http://localhost:3001`.

## Database Migrations

### Add a migration

```bash
cd src/R2WAI.Infrastructure
dotnet ef migrations add <MigrationName> -s ../R2WAI.Api/R2WAI.Api.csproj
```

### Apply migrations

```bash
cd src/R2WAI.Api
dotnet ef database update
```

### Revert a migration

```bash
dotnet ef migrations remove -s ../R2WAI.Api/R2WAI.Api.csproj
```

### Generate SQL script

```bash
dotnet ef migrations script -s ../R2WAI.Api/R2WAI.Api.csproj -o migration.sql
```

## Running Tests

### All tests

```bash
dotnet test R2WAI.slnx
```

### With coverage

```bash
dotnet test R2WAI.slnx --collect:"XPlat Code Coverage" --logger trx
```

### Integration tests (require PostgreSQL)

```bash
dotnet test R2WAI.slnx --filter Category=Integration
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=localhost;Database=r2wai;Username=postgres` | PostgreSQL connection string |
| `ConnectionStrings__Redis` | `localhost:6379` | Redis connection string |
| `Authentication__Jwt__SecretKey` | *dev key* | JWT signing key (min 32 chars) |
| `Authentication__Jwt__Issuer` | `R2WAI` | JWT issuer |
| `Authentication__Jwt__Audience` | `R2WAI-API` | JWT audience |
| `Authentication__EntraId__TenantId` | — | Azure Entra ID tenant |
| `Authentication__EntraId__ClientId` | — | Azure Entra ID client ID |
| `Storage__Provider` | `Local` | Storage provider: `Local` or `MinIO` |
| `Storage__MinIO__Endpoint` | `localhost:9000` | MinIO endpoint |
| `Storage__MinIO__AccessKey` | `minioadmin` | MinIO access key |
| `Storage__MinIO__SecretKey` | `minioadmin` | MinIO secret key |
| `VectorStore__Qdrant__Host` | `localhost` | Qdrant host |
| `VectorStore__Qdrant__Port` | `6333` | Qdrant gRPC port |
| `AI__OpenAI__ApiKey` | — | OpenAI API key |
| `AI__AzureOpenAI__Endpoint` | — | Azure OpenAI endpoint |
| `CORS__AllowedOrigins__0` | `https://localhost:3000` | Allowed CORS origin |

## Common Commands

### Backend

```bash
# Restore packages
dotnet restore R2WAI.slnx

# Build
dotnet build R2WAI.slnx

# Run API
dotnet run --project src/R2WAI.Api

# Run web app
dotnet run --project src/R2WAI.Web

# Watch mode
dotnet watch --project src/R2WAI.Api

# Run tests
dotnet test R2WAI.slnx

# Code format
dotnet format R2WAI.slnx
```

### Docker

```bash
# Start all services
docker compose -f docker/docker-compose.yml up -d

# Start specific service
docker compose -f docker/docker-compose.yml up -d postgres redis

# View logs
docker compose -f docker/docker-compose.yml logs -f r2wai-api

# Rebuild and start
docker compose -f docker/docker-compose.yml up -d --build

# Stop all
docker compose -f docker/docker-compose.yml down

# Clean volumes (destroys data)
docker compose -f docker/docker-compose.yml down -v
```

### Kubernetes (minikube or dev cluster)

```bash
# Deploy with kustomize
kubectl apply -k k8s/

# View pods
kubectl get pods -n r2wai

# Tail API logs
kubectl logs -n r2wai deployment/r2wai-api -f

# Port forward
kubectl port-forward -n r2wai service/r2wai-api 5000:80

# Scale
kubectl scale deployment/r2wai-api -n r2wai --replicas=3
```

## Code Quality

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add document summarization endpoint
fix: resolve tenant resolution race condition
chore: update NuGet packages
docs: document WebSocket reconnection strategy
```

### Branch Strategy

- `main` — production-ready code
- `develop` — integration branch
- `feat/*` — feature branches
- `fix/*` — bugfix branches
- `release/*` — release preparation

### PR Checklist

- [ ] Code builds without warnings
- [ ] Tests pass
- [ ] New endpoints documented in `docs/api/`
- [ ] Environment variables added to `appsettings.json`
- [ ] Database migration added if schema changed
- [ ] PR linked to issue

## Debugging

### SignalR / WebSocket

Open browser dev tools → Network → WS tab to inspect real-time frames.

### EF Core SQL Logging

Set `Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore` to `Information` in `appsettings.Development.json`.

### Qdrant

Access Qdrant dashboard at `http://localhost:6333/dashboard`.

### MinIO Console

Access MinIO console at `http://localhost:9001` (credentials: `minioadmin` / `minioadmin`).
