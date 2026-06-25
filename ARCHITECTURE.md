# R2WAI Architecture

> Enterprise AI Work Execution Platform — Architecture & Design Decisions

---

## System Overview

R2WAI is a modular enterprise platform that combines AI-powered assistants, visual workflow automation, and system integrations into a unified work execution engine. The architecture follows **Clean Architecture** principles with a **Blazor Server** frontend.

---

## High-Level Architecture

```text
┌─────────────────────────────────────────────┐
│                  R2WAI Portal               │
│               (Blazor Server)               │
└─────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────┐
│                  App Hub                    │
└─────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼

┌────────────────┐ ┌────────────────┐ ┌────────────────┐
│ AI Assistant   │ │ Workflow       │ │ Integrations   │
│ Studio         │ │ Studio         │ │ Studio         │
└────────────────┘ └────────────────┘ └────────────────┘

        ▼               ▼               ▼

┌────────────────┐ ┌────────────────┐
│ Operations     │ │ Settings       │
│ Center         │ │                │
└────────────────┘ └────────────────┘
```

### Studios

| Studio | Purpose |
|---|---|
| **AI Assistant Studio** | Create, configure, and deploy domain-specific AI assistants (HR, IT, Procurement, Finance, Legal). Uses Semantic Kernel + LLM providers. |
| **Workflow Studio** | Visual workflow designer powered by Elsa. Define approval chains, business actions, and automated pipelines. |
| **Integrations Studio** | Configure and manage connections to external systems (REST APIs, databases, email, etc.). |
| **Operations Center** | Monitor running workflows, review audit logs, inspect AI assistant activity, and manage approvals. |
| **Settings** | User/role management, tenant configuration, AI model settings, global preferences. |

---

## Logical Architecture

```text
Blazor Server UI
        │
        ▼
ASP.NET Core 10 API
(Clean Architecture)
        │
 ┌──────┼────────┬────────┬────────┐
 ▼      ▼        ▼        ▼        ▼

Application  Domain  Infrastructure  Shared
Layer        Layer   Layer           Kernel
```

### Layer Responsibilities

```
┌─────────────────────────────────────────────────┐
│              Presentation Layer                  │
│  (R2WAI.Api)                                     │
│  • REST Controllers  • SignalR Hubs              │
│  • Middleware         • API versioning            │
├─────────────────────────────────────────────────┤
│              Application Layer                   │
│  (R2WAI.Application)                             │
│  • CQRS / MediatR    • Feature Modules           │
│  • DTOs              • Validators                │
│  • Mappings          • Pipeline Behaviors        │
├─────────────────────────────────────────────────┤
│              Domain Layer                        │
│  (R2WAI.Domain)                                  │
│  • Entities          • Value Objects             │
│  • Enums             • Domain Events             │
│  • Interfaces        • Business Rules            │
├─────────────────────────────────────────────────┤
│           Infrastructure Layer                   │
│  (R2WAI.Infrastructure)                          │
│  • EF Core           • Semantic Kernel           │
│  • Qdrant            • Redis                     │
│  • MinIO             • Azure AD                  │
└─────────────────────────────────────────────────┘
```

---

## AI Layer

```text
AI Assistant Studio
        │
        ▼
Semantic Kernel
        │
 ┌──────┼──────────────┐
 ▼      ▼              ▼

Prompts  Tools  Knowledge Search
        │
        ▼
LLM Provider
(Ollama / OpenAI)
```

The AI layer is built on **Semantic Kernel**, providing:

- **Planner** — Auto-selects and chains tools to fulfill user requests
- **Plugins** — Tool registry for database queries, API calls, email, and workflow triggers
- **Memory** — Vector-based knowledge retrieval using Qdrant
- **Multi-Provider** — Supports Ollama (local, on-prem) and OpenAI/Azure OpenAI

### Supported Models (MVP)

| Provider | Model | Use Case |
|---|---|---|
| Ollama | Qwen2.5-Coder | Code generation, RFP responses, document processing |
| Ollama | Qwen2.5-7B/14B | General chat, assistant conversations |
| OpenAI | GPT-4o | Complex reasoning, summarization (optional) |
| Azure OpenAI | GPT-4o | Enterprise deployment with Entra ID auth |

---

## Workflow Layer

```text
Workflow Studio
        │
        ▼
Elsa Studio
        │
        ▼
Elsa Server
        │
        ▼
Workflow Runtime
        │
        ▼
Human Approvals  API Actions  Business Actions
```

Built on **Elsa 3.x** workflow engine:

- **Visual Designer** — Drag-and-drop workflow builder embedded in Blazor
- **Approval Steps** — Human-in-the-loop with email/SignalR notifications
- **Tool Integration** — Workflows can invoke AI assistants, send emails, call APIs, update databases
- **Audit Trail** — Every workflow execution is logged with full state history

### Workflow Patterns

| Pattern | Description |
|---|---|
| Sequential Approval | Step-by-step approval chain (e.g., Manager → Director → VP) |
| Parallel Review | Multiple reviewers simultaneously |
| Conditional Branching | Route based on document type, amount, department |
| AI-Assisted | AI pre-checks content, suggests decision, routes for human approval |
| Scheduled | Cron-triggered workflows (e.g., weekly report generation) |

---

## Tool Framework

```text
Assistant
      │
      ▼
Tool Registry
      │
 ┌────┼────┬────┬────┐
 ▼    ▼    ▼    ▼    ▼

Rest   DB   Email  Workflow  ...
API
```

Tools are the bridge between AI assistants and business actions. Each tool is a Semantic Kernel plugin that can be registered, configured, and invoked by assistants.

### Built-in Tools

| Tool | Capabilities |
|---|---|
| **REST API** | Call external APIs with configurable auth (Bearer, Basic, API Key) |
| **Database** | Execute parameterized queries against PostgreSQL |
| **Email** | Send email via SMTP; read/search mailbox |
| **Workflow** | Trigger Elsa workflows; check workflow status |
| **Knowledge** | Search knowledge bases; retrieve document chunks |
| **Document** | Upload, process, summarize, extract; compare documents |

---

## Knowledge Architecture

```text
Documents  Policies  Manuals  Contracts
      │
      ▼
Knowledge Indexer
      │
      ▼
PostgreSQL / Vector Store
      │
      ▼
Semantic Kernel Search
```

### Ingestion Pipeline

1. **Document Upload** — PDF, DOCX, TXT, Markdown via API or drag-and-drop
2. **Text Extraction** — OCR for scanned documents; structured extraction for tables
3. **Chunking** — Semantic chunking with configurable overlap and chunk size
4. **Embedding** — Generate vector embeddings using Ollama or OpenAI embedding models
5. **Storage** — Vectors in Qdrant (or pgvector for MVP simplicity); metadata in PostgreSQL
6. **Indexing** — Full-text search via PostgreSQL tsvector for hybrid search (dense + sparse)

### Search Types

| Type | Description |
|---|---|
| Dense (Vector) | Semantic similarity search via embeddings |
| Sparse (Keyword) | BM25-style full-text search via PostgreSQL tsvector |
| Hybrid | Combined dense + sparse with weighted rank fusion |
| Metadata Filtering | Filter by source, date, category, document type |

---

## Data Architecture

```text
PostgreSQL
│
├── Users              — User accounts, profiles, preferences
├── Roles              — Role definitions (Admin, Business User, Approver, Operator, Viewer)
├── Permissions        — Role-to-permission mappings
├── Assistants         — AI assistant configurations, prompts, tools
├── Workflows          — Elsa workflow definitions and metadata
├── Workflow Runs      — Execution history, state, variables
├── Approvals          — Pending and historical approval records
├── Documents          — Document metadata, status, processing results
├── Knowledge          — Knowledge base sources, chunks, embeddings
├── Integrations       — External system connection configurations
├── Audit Logs         — Immutable audit trail for all mutations
└── Settings           — Tenant-level and global application settings
```

### Data Storage Decisions

| Data | Store | Rationale |
|---|---|---|
| Relational data | PostgreSQL | ACID compliance, relationships, migrations |
| Vector embeddings | PostgreSQL (pgvector) | Single database for MVP; no separate Qdrant needed |
| File storage | Local filesystem (MVP) → MinIO (post-MVP) | Simplicity first; migrate when needed |
| Cache | In-memory (MVP) → Redis (post-MVP) | Reduces infra complexity for MVP |
| Chat history | PostgreSQL | Always persisted; no ephemeral state |

---

## Security Architecture

```text
Microsoft Entra ID
        │
        ▼
JWT Authentication
        │
        ▼
Role-Based Authorization
        │
        ▼
Audit Logging
```

### Authentication

| Method | Purpose |
|---|---|
| **Microsoft Entra ID** | Enterprise SSO — primary auth for production |
| **JWT (Local)** | Development, testing, and non-Entra deployments |
| **API Key** | Service-to-service communication (Integrations) |

### Role Model

| Role | Scope | Permissions |
|---|---|---|
| **Administrator** | Global | Full access — users, settings, all studios |
| **Business User** | Self + Team | Use assistants, manage own workflows, upload documents |
| **Approver** | Departmental | Approve/reject workflows assigned to their scope |
| **Operator** | Global (Read) | View all workflows, audits, system status (no mutations) |
| **Viewer** | Self (Read) | View their own conversations and workflow history |

### Security Principles

- **JWT tokens** signed with 256-bit key, short expiry (15 min) + refresh tokens
- **Entra ID conditional access** for production deployments
- **Input validation** on all API endpoints via FluentValidation
- **SQL injection prevention** via EF Core parameterized queries
- **Audit logging** on every mutation (Create, Update, Delete) with user ID, timestamp, IP, and before/after state
- **Secrets never logged** — Serilog destructuring excludes sensitive fields
- **Rate limiting** — 100 req/min per authenticated user; 20 req/min anonymous

---

## Request Flow

```text
User
 │
 ▼
AI Assistant
 │
 ▼
Semantic Kernel
 │
 ▼
Select Tool
 │
 ▼
Elsa Workflow
 │
 ▼
Approval
 │
 ▼
Execute Action
 │
 ▼
Business Outcome
```

### Example: RFP Response

1. User uploads RFP document via AI Assistant Studio
2. Semantic Kernel extracts requirements and generates draft response using Qwen2.5-Coder
3. Tool "Proposal" triggers an Elsa workflow
4. Workflow routes to Approver (e.g., VP of Sales)
5. Approver reviews and approves via Operations Center
6. Workflow executes: saves final proposal, emails stakeholders, updates CRM

---

## Deployment Architecture (MVP)

```text
Docker Host
│
├── R2WAI Web         — Blazor Server + API (single container)
├── PostgreSQL         — Database + pgvector
├── Ollama             — Local LLM inference
└── Elsa Runtime      — Workflow execution server
```

### MVP vs Post-MVP

| Component | MVP | Post-MVP |
|---|---|---|
| **Deployment** | Docker Compose | Kubernetes (AKS) |
| **LLM** | Ollama (local) | Ollama + OpenAI/Azure OpenAI |
| **Vector Store** | pgvector (in PostgreSQL) | Qdrant (dedicated, scalable) |
| **Cache** | In-memory | Redis |
| **File Storage** | Local volume | MinIO (S3-compatible) |
| **Auth** | JWT only | JWT + Entra ID |
| **Monitoring** | Serilog + console | Prometheus + Grafana |
| **Scaling** | Single instance | Horizontal pod autoscaling |

---

## Technology Stack

| Layer | Technology |
|---|---|
| **Backend** | .NET 10, ASP.NET Core, C# |
| **Frontend** | Blazor Server, MudBlazor |
| **Real-Time** | SignalR |
| **AI** | Semantic Kernel, Ollama, OpenAI |
| **Workflow** | Elsa 3.x (Server + Studio) |
| **Database** | PostgreSQL 16 (EF Core + pgvector) |
| **Auth** | JWT, Microsoft Entra ID |
| **Infrastructure** | Docker |
| **CI/CD** | GitHub Actions |

---

## Project Structure

```
R2WAI/
├── src/
│   ├── R2WAI.Api/              # ASP.NET Core API (Controllers, Hubs, Middleware)
│   ├── R2WAI.Application/      # CQRS commands, queries, DTOs, mappings
│   ├── R2WAI.Domain/           # Entities, value objects, enums, events
│   ├── R2WAI.Infrastructure/   # EF Core, Semantic Kernel, Qdrant, Redis, MinIO
│   └── R2WAI.Web/              # Blazor Server app with MudBlazor
├── docker/                     # Dockerfiles and docker-compose
├── k8s/                        # Kubernetes manifests (post-MVP)
├── .github/workflows/          # CI/CD pipelines
├── docs/                       # Documentation
│   ├── api/                    # API reference
│   ├── deployment/             # Deployment guide
│   └── development/            # Developer setup
├── tests/                      # Test projects
├── ARCHITECTURE.md             # This document
├── README.md                   # Project overview
└── ROADMAP.md                  # Development roadmap
```

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| **Frontend** | Blazor Server (not WASM) | Faster MVP delivery; SignalR built-in; no API token management in browser |
| **UI Kit** | MudBlazor | Comprehensive component library; matches Blazor Server lifecycle |
| **Workflow** | Elsa 3.x (not Temporal/Camunda) | .NET-native; embedded designer; no additional infra; MIT license |
| **AI Orchestration** | Semantic Kernel (not LangChain) | First-class .NET support; plugin model; Microsoft ecosystem |
| **LLM Provider** | Ollama first (not OpenAI-only) | On-prem requirement; no data leaves network; cost control |
| **Database** | Single PostgreSQL with pgvector | One database for MVP; eliminate Qdrant infra complexity |
| **Architecture** | Clean Architecture (not Vertical Slices) | Team familiarity; established patterns; clear separation of concerns |
| **CQRS** | MediatR | Decouples commands/queries; pipeline behaviors for cross-cutting concerns |
| **Auth (MVP)** | JWT only | Simple; no Entra ID dependency for local dev |
| **Deployment (MVP)** | Docker Compose (not K8s) | Minimal ops overhead for 1-5 person team |

---

## Related Documents

| Document | Description |
|---|---|
| [README.md](README.md) | Project overview, quick start, feature modules |
| [ROADMAP.md](ROADMAP.md) | 24-week development roadmap with milestones |
| [docs/api/API.md](docs/api/API.md) | Complete API reference with endpoints and examples |
| [docs/deployment/DEPLOYMENT.md](docs/deployment/DEPLOYMENT.md) | Production deployment guide |
| [docs/development/DEVELOPMENT.md](docs/development/DEVELOPMENT.md) | Local development setup and common commands |
