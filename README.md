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

## Step-by-Step User Guide

A walkthrough for end users (not developers) of the deployed application, organized by the four top-level areas of the app: **Studio** (build), **Workspace** (daily use), **Monitor** (oversight), **Settings** (admin).

1. **Sign in** — Log in with email/password or SSO (Azure Entra ID); complete MFA if your tenant requires it.
2. **Upload documents** (*Workspace → Documents*) — Drag-and-drop or select files (PDF, DOCX, XLSX, PPTX, TXT, CSV, MD, images; up to 50 MB each, 20 at a time). Status moves Uploading → Processing → Ready. Once ready, you can summarize, extract from, compare, or ask questions about a document.
3. **Organize a Knowledge Base** (*Studio → Knowledge Bases*) — Group related documents together; this is the searchable RAG collection an assistant draws on, with source citations.
4. **Create an AI Assistant** (*Studio → Assistants*) — Describe it in plain language (e.g. "Create an HR assistant that helps with onboarding") or start from a template (HR, IT, Finance, Legal, Procurement). Attach a Knowledge Base, set tone/instructions and model, test it in the Playground, then publish.
5. **Chat with an assistant** (*Workspace → Conversations*) — Pick an assistant and chat in real time (streaming responses); history is searchable and exportable.
6. **Automate a process** (*Studio → Workflows*) — Build visually, generate from a natural-language description, or start from a template (Invoice Approval, Leave Request, Expense Report, Employee Onboarding). Define approval steps, escalation rules, and SLA timers, then activate it.
7. **Handle approvals** (*Workspace → Inbox / Approvals*) — Review items routed to you with full context (requester, amount, due date); approve, reject, or comment. Overdue and escalated items are flagged.
8. **Deploy a chatbot** (*Studio → Chatbots*, optional) — Wrap an assistant + knowledge base into an embeddable widget for your website; grab the embed code.
9. **Generate an RFP response** (*Workspace → Proposals*, optional) — Paste in RFP requirements and get an AI-drafted response built from your knowledge base and templates, then edit it.
10. **Monitor health** (*Monitor*) — Dashboard KPIs, AI usage/cost, analytics, reports, and the immutable audit trail (who did what, when).
11. **Administer the tenant** (*Settings*, admin only) — Manage users, roles and permissions, security/MFA, AI model providers and API keys, webhooks, and content moderation.

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
