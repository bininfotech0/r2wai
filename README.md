# R2WAI

Enterprise AI work-execution platform: AI agents, knowledge (RAG), connections, automations and
embeddable chatbots, self-hosted and multi-tenant.

**Target journey:** Connect → Create Agent → Configure → Test → Publish → Monitor

## Repository layout

```
src/
  R2WAI.Api             ASP.NET Core API
  R2WAI.Application     Use cases (CQRS), AI abstractions
  R2WAI.Domain          Entities and domain rules
  R2WAI.Infrastructure  EF Core, AI runtime, integrations
  R2WAI.Client          React/TypeScript studio (Playwright specs in e2e/)
  R2WAI.Widget          Embeddable chat widget
tests/
  R2WAI.*.Tests         .NET unit/integration tests (run with dotnet test)
  e2e/                  Node/Playwright browser scripts against a running instance
docs/                   Product, architecture, ADRs, API, deployment, audits (see docs/README.md)
AI/                     Rules and standards for AI coding agents
docker/, k8s/           Container and Kubernetes deployment
ROADMAP.md              Current product roadmap and target architecture
```

## Quick start

```sh
docker compose -f docker/docker-compose.yml up -d postgres redis qdrant minio
dotnet run --project src/R2WAI.Api
cd src/R2WAI.Client && npm install && npm run dev
```

See [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) for the full setup.

## Tests

```sh
dotnet test R2WAI.slnx                         # .NET tests
cd src/R2WAI.Client && npm test                # client unit tests
npm run e2e:pages                              # browser scripts, see tests/e2e/README.md
```

## Documentation

Start at [docs/README.md](docs/README.md).
