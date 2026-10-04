# R2WAI Production Deployment Runbook

## Prerequisites

- Docker Engine 24+ with Docker Compose v2
- 4 GB RAM minimum (8 GB recommended)
- 20 GB disk space
- OpenAI API key
- (Optional) SMTP credentials for email notifications
- (Optional) Azure Entra ID app registration for SSO

## Step 1: Clone and Configure

```bash
git clone <repo-url> r2wai
cd r2wai/docker
cp .env.production.example .env
```

Edit `.env` and fill in all required values:
- `DB_PASSWORD` — strong random password (min 16 chars)
- `JWT_SECRET` — generate with `openssl rand -base64 64`
- `ENCRYPTION_KEY` — generate with `openssl rand -base64 32`
- `OPENAI_API_KEY` — your OpenAI API key

## Step 2: Build and Start

```bash
docker compose -f docker-compose.production.yml up -d --build
```

Wait for all services to become healthy (~60 seconds):

```bash
docker compose -f docker-compose.production.yml ps
```

Expected output: all services show `Up (healthy)`.

## Step 3: Verify

```bash
# Check API health
curl http://localhost:5000/health/ready

# Check web UI
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080
```

Navigate to `http://localhost:8080` in a browser.

## Step 4: Initial Setup

1. Login with the administrator you configured for the first start. On an **empty** database the API
   creates the default tenant, roles and built-in tools plus one administrator from
   `BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PASSWORD` (`Bootstrap__AdminEmail` /
   `Bootstrap__AdminPassword`; password of at least 12 characters, and not one of the demo passwords
   published in the repository). Then **remove `BOOTSTRAP_ADMIN_PASSWORD` from the environment**. If no
   administrator is configured, no user is created — the startup log says so.
   *No demo accounts are created in production.* The demo seed (`admin@r2wai.io`, `user@r2wai.io`, …
   with public passwords) exists only for local development and CI and is opt-in:
   `Database__SeedDemoData=true` (on by default only when `ASPNETCORE_ENVIRONMENT=Development`; set
   explicitly in `docker/docker-compose.yml`). Never enable it on a real deployment.
2. Navigate to Settings → Users to create additional users
3. Navigate to Settings → AI Models to configure your LLM provider
4. Navigate to AI Assistant Studio to create your first assistant

## Step 5: Configure Backups

Set up daily automated backups:

```bash
# Linux/Mac: Add to crontab
crontab -e
# Add: 0 2 * * * /path/to/r2wai/docker/backup.sh >> /var/log/r2wai-backup.log 2>&1

# Verify backup works
chmod +x backup.sh restore.sh
./backup.sh
```

## Monitoring

### Health Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health` | Full health check |
| `GET /health/ready` | Readiness probe (DB + services) |
| `GET /health/startup` | Startup check |

### Logs

```bash
# API logs
docker compose -f docker-compose.production.yml logs -f r2wai-api

# Studio (React SPA) logs
docker compose -f docker-compose.production.yml logs -f r2wai-studio

# Database logs
docker compose -f docker-compose.production.yml logs -f postgres
```

Application logs are also stored in the `api-logs` volume at `/app/Logs/`.

## Common Operations

### Restart Services

```bash
docker compose -f docker-compose.production.yml restart r2wai-api
docker compose -f docker-compose.production.yml restart r2wai-studio
```

### Apply Database Migrations

Migrations run automatically on startup. To run manually:

```bash
docker compose -f docker-compose.production.yml exec r2wai-api dotnet ef database update
```

### Restore from Backup

```bash
./restore.sh backups/r2wai_backup_YYYYMMDD_HHMMSS.sql.gz
```

### Scale Down / Stop

```bash
docker compose -f docker-compose.production.yml down
# Data persists in Docker volumes

# To also remove volumes (DESTRUCTIVE):
# docker compose -f docker-compose.production.yml down -v
```

## Troubleshooting

| Symptom | Check | Fix |
|---|---|---|
| API won't start | `docker logs r2wai-api` | Check DB connection string, JWT secret |
| DB connection refused | `docker ps` — is postgres healthy? | Wait for health check or restart postgres |
| Login fails | Check JWT_SECRET matches between deploys | Ensure .env is consistent |
| Emails not sending | Check SMTP config in .env | Verify SMTP credentials, check firewall |
| AI not responding | Check OPENAI_API_KEY | Verify key is valid, check quota |
