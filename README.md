# R2WAI — Enterprise AI Work Execution Platform

> AI-powered platform for RFP response generation, knowledge management, enterprise chatbots, document processing, and automated workflows.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Layer                               │
│    Blazor Server App   │   Elsa Studio   │   API Clients      │
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
│    EF Core   │   Semantic Kernel   │   Qdrant   │   Redis     │
│    MinIO     │   Azure AD          │   SignalR                │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

| Layer | Technology |
|---|---|
| **Backend** | .NET 10, ASP.NET Core, C# |
| **Frontend** | Blazor Server, MudBlazor |
| **Real-Time** | SignalR (WebSocket) |
| **AI** | Semantic Kernel, OpenAI / Azure OpenAI |
| **Database** | PostgreSQL 16 (EF Core) |
| **Cache** | Redis 7 |
| **Vector Store** | Qdrant |
| **Object Storage** | MinIO |
| **Auth** | JWT, Azure Entra ID |
| **CI/CD** | GitHub Actions |
| **Container** | Docker, Kubernetes (AKS) |
| **Monitoring** | Serilog, Prometheus, Grafana |

## Features

- **AI Assistant Studio** — Create domain-specific assistants (HR, IT, Finance, Legal, Procurement) with natural language or templates, powered by configurable LLMs
- **RAG Knowledge Bases** — Upload documents (PDF, DOCX, XLSX), process and embed them with pgvector, and enable semantic search with source citations
- **Workflow Automation** — Design and execute multi-step business workflows with Elsa 3.x, including approval chains, scheduling, and visual process tracking
- **Approval Engine** — Policy-based approval routing with multi-level chains, SLA tracking, escalation, and real-time notifications
- **Enterprise Chatbots** — Build embeddable website chatbots backed by AI assistants and knowledge bases
- **Integrations Marketplace** — Connect 20+ external systems (Salesforce, Slack, Jira, GitHub, etc.) with REST API connectors and OAuth2 support
- **Operations Center** — Monitor platform health, view AI usage analytics, generate cost/usage/compliance/assistant reports, and track audit logs
- **Multi-Tenancy** — Isolated workspaces with role-based access control, configurable permissions, and feature flags per tenant
- **Real-Time Streaming** — SSE-based chat streaming and SignalR live activity feeds
- **Enterprise Audit Trail** — Immutable audit logging for all mutations with correlation tracking and CSV/JSON export

## Screenshots

| Dashboard | Assistant Studio | Workflow Builder |
|-----------|-----------------|-----------------|
| ![Dashboard](docs/screenshots/dashboard.png) | ![Assistants](docs/screenshots/assistants.png) | ![Workflows](docs/screenshots/workflows.png) |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16](https://www.postgresql.org/) (or use Docker)
- [Docker](https://www.docker.com/) (for infrastructure services)

```bash
# 1. Start infrastructure
docker compose -f docker/docker-compose.yml up -d postgres redis qdrant minio

# 2. Start backend
cd src/R2WAI.Api
dotnet run

# 3. Start web app (separate terminal)
cd src/R2WAI.Web
dotnet run
```

- **API**: `http://localhost:5000` — Swagger at `/swagger`
- **Web App**: `http://localhost:3001` / `https://localhost:3000`
- **MinIO Console**: `http://localhost:9001` (`minioadmin` / `minioadmin`)
- **Qdrant Dashboard**: `http://localhost:6333/dashboard`

## Project Structure

```
R2WAI/
├── src/
│   ├── R2WAI.Api/                # ASP.NET Core API (Controllers, Hubs, Middleware)
│   ├── R2WAI.Application/        # CQRS commands, queries, DTOs, mappings
│   ├── R2WAI.Domain/             # Entities, value objects, enums, events
│   └── R2WAI.Infrastructure/     # EF Core, Semantic Kernel, Qdrant, Redis, MinIO
├── src/R2WAI.Web/                # Blazor Server app with MudBlazor
│   ├── Components/               # Layouts, pages, and shared UI
│   ├── Authentication/           # JWT auth state and API handler
│   ├── Services/                 # Web app services
│   └── wwwroot/                  # Static assets
├── docker/                       # Dockerfiles and docker-compose
├── k8s/                          # Kubernetes manifests (kustomize)
├── .github/workflows/            # CI/CD pipelines
└── docs/                         # Documentation
    ├── api/                      # API reference
    ├── deployment/               # Deployment guide
    └── development/              # Developer setup guide
```

## Documentation

| Document | Description |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture, design decisions, data model |
| [ROADMAP.md](ROADMAP.md) | 24-week development roadmap with milestones |
| [docs/api/API.md](docs/api/API.md) | Complete API reference with endpoints and examples |
| [docs/deployment/DEPLOYMENT.md](docs/deployment/DEPLOYMENT.md) | Production deployment guide |
| [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) | Local development setup and common commands |

## Feature Modules

- **Chat** — Real-time AI chat with streaming, conversation management
- **Documents** — Upload, process, summarize, extract, compare
- **Knowledge Bases** — RAG-powered semantic search with source citations
- **Chatbots** — Embeddable website chatbots with training
- **Proposals** — AI-driven RFP response generation
- **Workflows** — Automated approval and action workflows
- **Assistants** — Domain-specific enterprise assistants (HR, IT, Procurement, Finance, Legal)
- **Admin** — User/role management, audit logs, analytics, model config

## Deployment

### Docker Compose

```bash
docker compose -f docker/docker-compose.yml up -d
```

### Kubernetes

```bash
kubectl apply -k k8s/
```

See [docs/deployment/DEPLOYMENT.md](docs/deployment/DEPLOYMENT.md) for full production deployment guide.

## Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Required |
| `ConnectionStrings__Redis` | Redis connection string | `localhost:6379` |
| `Jwt__Secret` | JWT signing key (min 32 chars) | Required |
| `Jwt__Issuer` | JWT issuer | `R2WAI` |
| `OpenAI__ApiKey` | OpenAI API key for AI features | Optional |
| `OpenAI__Model` | Default model ID | `gpt-4o` |
| `Storage__Provider` | Storage backend (`Local` or `MinIO`) | `Local` |
| `Authentication__EntraId__TenantId` | Azure Entra ID tenant for SSO | Optional |

See [docker/.env.example](docker/.env.example) for a complete list.

## Contributing

1. Fork the repository
2. Create a feature branch (`feat/your-feature`)
3. Commit with [Conventional Commits](https://www.conventionalcommits.org/)
4. Open a pull request to `develop`

See [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) for setup instructions and coding conventions.

## License

Proprietary — All rights reserved.

---

*Built with .NET, Blazor Server, MudBlazor, Semantic Kernel, and Elsa*
