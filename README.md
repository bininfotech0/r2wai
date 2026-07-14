# R2WAI — Enterprise AI Work Execution Platform

> AI-powered platform for knowledge management, enterprise chatbots, document processing, and automated approval workflows.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Layer                               │
│         Blazor Server App        │        API Clients         │
├─────────────────────────────────────────────────────────────┤
│                   API Gateway (nginx / AFD)                   │
├─────────────────────────────────────────────────────────────┤
│           Presentation Layer (R2WAI.Api)                     │
│    REST Controllers   │   SignalR Hubs   │   Middleware       │
├─────────────────────────────────────────────────────────────┤
│           Application Layer (R2WAI.Application)              │
│    CQRS / MediatR   │   Feature Modules   │   Validators      │
├─────────────────────────────────────────────────────────────┤
│            Domain Layer (R2WAI.Domain)                       │
│    Entities   │   Value Objects   │   Enums   │   Events      │
├─────────────────────────────────────────────────────────────┤
│        Infrastructure Layer (R2WAI.Infrastructure)           │
│  EF Core   │  Semantic Kernel  │  pgvector  │  Redis (opt.)   │
│  MinIO (opt.)  │  Azure Entra ID  │  SignalR                  │
└─────────────────────────────────────────────────────────────┘
```

Dependencies point inward (Clean Architecture): `Web → Api → Application → Domain`, with `Infrastructure` implementing `Application`'s interfaces against `Domain`. Workflow execution is backed by the [Elsa](https://elsa-workflows.github.io/elsa-documentation/) 3.x engine, embedded directly in `R2WAI.Api` — there is no separate workflow-designer service.

## Technology Stack

| Layer | Technology |
|---|---|
| **Backend** | .NET 10, ASP.NET Core, C# |
| **Frontend** | Blazor Server, MudBlazor |
| **Real-Time** | SignalR (WebSocket), Server-Sent Events (AI streaming) |
| **AI** | Semantic Kernel, OpenAI / Ollama / Z.ai (OpenAI-compatible) |
| **Workflow Engine** | Elsa 3.x (embedded in the API) |
| **Database** | PostgreSQL 16 (EF Core) |
| **Vector Store** | pgvector (PostgreSQL extension) |
| **Cache** | Redis (optional — falls back to in-memory) |
| **Object Storage** | Local disk or MinIO (configurable) |
| **Auth** | JWT, Azure Entra ID, TOTP MFA |
| **CI/CD** | GitHub Actions |
| **Container** | Docker (Docker Compose) |
| **Monitoring** | Serilog, OpenTelemetry |

## Features

- **AI Assistants** — Create domain-specific assistants (HR, IT, Finance, Legal, Procurement) with configurable LLM providers, instructions, and attached knowledge bases
- **RAG Knowledge Bases** — Upload documents, process and embed them with pgvector, and enable semantic search with source citations
- **Workflow Automation** — Design and execute multi-step business workflows on the Elsa engine, including approval chains, scheduling, and transform/notification steps
- **Approval Engine** — Policy-based multi-level approval routing with SLA tracking, escalation, and real-time notifications
- **Enterprise Chatbots** — Build embeddable, multi-channel website chatbots backed by AI assistants and knowledge bases (anonymous visitors never get tool-calling access — see Security notes below)
- **Integrations** — Configure external system connections (REST APIs, email) callable by AI assistants via the tool framework
- **Operations Center** — Monitor platform health, view AI usage, generate reports, and track the immutable audit trail
- **Multi-Tenancy** — Isolated workspaces enforced via EF Core global query filters, with role-based access control
- **Real-Time Streaming** — SSE-based chat streaming and SignalR live activity/notification feeds
- **Voice** — Voice-enabled chat sessions for chatbots and assistants

## Security notes

- Anonymous chatbot-widget traffic is served with Semantic Kernel's mutating tool plugins (workflow start, approval actions) explicitly disabled — see `SemanticKernelService.GetOrCreateKernel(enableTools)` and its callers in `ChatbotsController`.
- Multi-tenancy is enforced at the data layer (EF Core global query filters keyed on `TenantId`), not just in application logic.
- JWT is the primary auth scheme; Elsa's own workflow engine shares the same signing key rather than registering a competing scheme.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for PostgreSQL/pgvector, or run your own Postgres 16+ with the `pgvector` extension)

### Local development

```bash
# 1. Start Postgres (pgvector-enabled)
docker compose -f docker/docker-compose.yml up -d postgres

# 2. Start backend
cd src/R2WAI.Api
dotnet run

# 3. Start web app (separate terminal)
cd src/R2WAI.Web
dotnet run
```

- **API**: Swagger UI at `/swagger` (Development/Staging only)
- **Web App**: Blazor Server UI
- Redis and MinIO are optional — the app falls back to in-memory cache and local disk storage when they're not configured (`Cache:Redis:ConnectionString`, `Storage:Provider` in `appsettings.json`)

### Full stack via Docker Compose

```bash
docker compose -f docker/docker-compose.yml up -d
```

Brings up `r2wai-web`, `r2wai-api`, and `postgres` (the `pgvector/pgvector:pg16` image). See `docker/.env.example` for required environment variables.

## Project Structure

```
R2WAI/
├── src/
│   ├── R2WAI.Api/                # ASP.NET Core API (Controllers, Hubs, Middleware, Elsa activities)
│   ├── R2WAI.Application/        # CQRS commands/queries, DTOs, interfaces, validation behaviors
│   ├── R2WAI.Domain/             # Entities, value objects, enums, domain events (no external deps)
│   ├── R2WAI.Infrastructure/     # EF Core, Semantic Kernel, pgvector, storage, cache, auth
│   └── R2WAI.Web/                # Blazor Server app with MudBlazor
│       ├── Components/           # Layouts, pages, dialogs, shared UI
│       ├── Authentication/       # JWT auth state
│       ├── Services/             # Web app services (HTTP clients, chat/voice sessions)
│       └── wwwroot/              # Static assets
├── tests/                        # xUnit test projects (one per src project) + Playwright/browser e2e scripts
├── docker/                       # Dockerfiles, docker-compose, backup/restore scripts
├── .github/workflows/            # CI/CD pipelines
└── docs/                         # Documentation (API reference, deployment, development guides)
```

> Kubernetes manifests are not part of this repo — Docker Compose is the deployment target (per `docs/implementation/MVP-IMPLEMENTATION-PLAN.md`). `docs/deployment/DEPLOYMENT.md` still describes a `kubectl`/`k8s/` flow from an earlier plan; treat that section as aspirational, not current.

## Documentation

| Document | Description |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture and design decisions |
| [docs/api/API.md](docs/api/API.md) | API reference with endpoints and examples |
| [docs/deployment/DEPLOYMENT.md](docs/deployment/DEPLOYMENT.md) | Production deployment guide |
| [docs/deployment/RUNBOOK.md](docs/deployment/RUNBOOK.md) | Operational runbook |
| [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) | Local development setup and common commands |

> Some documents in `docs/` are point-in-time snapshots (dated in their own headers) rather than living docs — check the date before relying on specifics.

## Feature Modules

Application-layer feature areas (`src/R2WAI.Application/Features/`), each with its own controller in `R2WAI.Api/Controllers/`:

- **Chat** — Real-time AI chat with streaming, conversation management
- **Documents** — Upload, process, summarize, extract, compare
- **KnowledgeBases** — RAG-powered semantic search with source citations
- **Chatbots** — Embeddable, multi-channel website chatbots
- **Workflows** — Automated approval and action workflows (Elsa-backed)
- **Assistants** — Domain-specific enterprise assistants
- **Integrations** — External system connections and the tool framework
- **Admin** — User/role management, model configuration, audit logs
- **Operations** — Health monitoring, usage analytics, reporting

Additional controllers not tied to an Application feature folder: `Auth`, `ApiKeys`, `Schedules`, `Webhooks`.

## Deployment

### Docker Compose

```bash
docker compose -f docker/docker-compose.yml up -d
```

Docker Compose is the current deployment target — see [docs/deployment/DEPLOYMENT.md](docs/deployment/DEPLOYMENT.md) for the full guide.

## Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Required |
| `ConnectionStrings__Redis` / `Cache__Redis__ConnectionString` | Redis connection string | Unset → in-memory cache |
| `Authentication__Jwt__SecretKey` | JWT signing key (min 32 chars) | Required |
| `Authentication__Jwt__Issuer` | JWT issuer | `R2WAI` |
| `AI__Provider` | AI provider (`openai`, `ollama`, or `zai`) | `openai` |
| `AI__OpenAI__ApiKey` | OpenAI API key | Required if provider is `openai` |
| `Storage__Provider` | Storage backend (`Local` or `MinIO`) | `Local` |
| `Authentication__EntraId__TenantId` | Azure Entra ID tenant for SSO | Optional |

See `docker/.env.example` and `docker/.env.production.example` for complete lists.

## Contributing

1. Fork the repository
2. Create a feature branch (`feat/your-feature`)
3. Commit with [Conventional Commits](https://www.conventionalcommits.org/)
4. Open a pull request

See [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) for setup instructions and coding conventions.

## License

Proprietary — All rights reserved.

---

*Built with .NET, Blazor Server, MudBlazor, Semantic Kernel, and Elsa*
