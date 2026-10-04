# DevOps & Deployment Standards

## Deployment models

### SaaS

Managed R2WAI infrastructure.

### Private

Customer infrastructure, preferably Docker-first.

Both should share core application concepts.

## Local development

Use the repository's actual:
- Docker Compose
- environment files
- startup commands
- dependency services

Do not invent commands. Inspect the repository.

## Production concerns

Validate:
- HTTPS/TLS
- secrets
- health checks
- readiness checks
- backups
- restore
- logging
- monitoring
- resource limits
- persistent storage
- database migrations
- rollback

## Containers

Containers should:
- be reproducible
- avoid embedded secrets
- have health checks where appropriate
- use explicit configuration
- run with least privilege where practical

## Kubernetes

Kubernetes is not a launch requirement unless the customer/deployment requirement demands it.

If introduced:
- document architecture
- define scaling
- define secrets
- define persistence
- define ingress
- define observability
- define rollback

## Deployment checklist

```text
Build
→ Scan
→ Test
→ Package
→ Deploy
→ Health check
→ Smoke test
→ Monitor
→ Rollback if required
```
