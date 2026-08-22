# R2WAI Architecture

> System architecture and design decisions, kept in sync with the codebase (not a roadmap or audit snapshot).
> This document describes the **target architecture**: an Application-centric Government AI Platform. Sections marked **[target]** describe the intended end-state; everything else describes what is already implemented. See [Adoption Status](#adoption-status) for the current codebase → target delta.

---

## System Overview

R2WAI is a self-hosted, multi-tenant platform for building **AI assistants on top of existing government applications**. It connects to an existing application, automatically discovers its APIs, navigation, and capabilities, then generates the assistant, knowledge base, tools, permissions, and workflows — leaving the admin to review, test, and publish.

The backend follows **Clean Architecture**; the frontend is **Blazor Server**. AI orchestration uses **Semantic Kernel**; workflows and human-in-the-loop approvals use **Elsa** behind R2WAI's own workflow abstraction.

**The strategic pivot:** R2WAI is not a collection of independent AI features and "studios." It is an **Application-centric platform**. The central domain entity is `Application` (the external system being connected). Everything else — APIs, assistant, knowledge, navigation, tools, workflows, policies, security, monitoring — is configured **per application**.

## Strategic Direction

### What changes

```text
OLD (feature collection)
 Tenant → Assistant, Chatbot, KnowledgeBase, Workflow, Model

NEW (application platform)
 Tenant → Department → Application
                       ├── APIs
                       ├── Assistant
                       ├── Knowledge
                       ├── Navigation
                       ├── Tools
                       ├── Workflows
                       ├── Policies
                       ├── Model
                       ├── Security
                       └── Monitoring
```

### The three highest-value additions

1. **Application as the central domain entity**
2. **Application Discovery Engine + adaptive low-code wizard** (connect → auto-discover → auto-configure → review → test → publish)
3. **Secure API/Tool Gateway with RBAC + ABAC + policy enforcement** (the LLM never calls government APIs directly)

### The three biggest removals

1. Separate `Chatbot`/`Assistant`/`Agent` concepts → merged into **Assistant + Channels**
2. Unnecessary standalone studios (Chatbot, Model, Integration, Navigation, Tool, Media) → folded into application areas or Administration
3. Media/creative-generation features → removed from the government core product

---

## Solution Layout

Solution file: `src/R2WAI.slnx` (used by CI and local builds).

| Project | Role | Depends on |
|---|---|---|
| `R2WAI.Domain` | Entities, value objects, enums, domain events | — (no project references) |
| `R2WAI.Application` | CQRS commands/queries (MediatR), DTOs, interfaces, validation/logging pipeline behaviors | `R2WAI.Domain` |
| `R2WAI.Infrastructure` | EF Core persistence, Semantic Kernel AI, storage, cache, auth, tool framework | `R2WAI.Application`, `R2WAI.Domain` |
| `R2WAI.Api` | ASP.NET Core host: controllers, SignalR hubs, Elsa workflow engine, middleware | `R2WAI.Application`, `R2WAI.Infrastructure` |
| `R2WAI.Web` | Blazor Server UI (MudBlazor) | `R2WAI.Api` (project reference) |

Dependencies point inward — `Web → Api → Application → Domain`, with `Infrastructure` implementing `Application`'s interfaces against `Domain`. `R2WAI.Domain` has zero external project references by design.

Test projects mirror the source layout under `tests/` (`R2WAI.Domain.Tests`, `R2WAI.Application.Tests`, `R2WAI.Infrastructure.Tests`, `R2WAI.Api.Tests`), plus Playwright/browser end-to-end scripts under `tests/e2e/` and `tests/*.mjs`.

## Layer Details

### R2WAI.Api

- **Controllers** (`Controllers/`): `Admin`, `ApiKeys`, `Approvals`, `Assistants`, `Auth`, `Chat`, `Chatbots`, `Documents`, `Integrations`, `KnowledgeBases`, `Operations`, `Schedules`, `Webhooks`, `Workflows`. **[target]** `Chatbots` controllers collapse into `Assistant` + `Channels`; `Applications`, `Discovery`, `Gateway`/`Tools`, `Governance` controllers are added.
- **Hubs** (`Hubs/`): `ChatHub` (`/hubs/chat`), `StatusHub` (`/hubs/status`), `NotificationHub` (`/hubs/notification`) — all `[Authorize]`, token accepted via `?access_token=` query string for SignalR clients.
- **Workflows** (`Workflows/`): Elsa custom activities — `ApprovalStepActivity`, `InvokeSemanticKernelActivity`, `TransformStepActivity`, `StepActivityFactory`, `StepStatusNotificationHandler` — bridging the app's own `Workflow`/`WorkflowInstance` model onto the Elsa runtime via `IWorkflowBridge`.
- **Middleware** (`Middleware/`): correlation ID, exception handling, request logging, rate limiting, security headers, tenant resolution, API-key authentication. **[target]** an application/tool **gateway middleware** is added in front of any outbound LLM-invoked tool call.
- **Program.cs**: composition root. Serilog logging with a sensitive-data enricher; Elsa workflow management + runtime registered against the same PostgreSQL connection string as the app's own `ApplicationDbContext`; JWT Bearer auth; authorization policies (`AdminOnly`, `TenantAccess`, `CanManageUsers`, `CanManageDocuments`, `CanManageWorkflows`); CORS allow-list; OpenTelemetry tracing/metrics; Swagger (Dev/Staging only); health checks at `/health`, `/health/ready`, `/health/startup`; a `ValidateProductionConfig` guard that refuses to start in Production if secrets are still `CHANGE_ME` placeholders.

### R2WAI.Application

CQRS via MediatR under `Features/{Area}/{Commands,Queries,DTOs}`. Areas: `Admin`, `Assistants`, `Chat`, `Chatbots`, `Documents`, `Integrations`, `KnowledgeBases`, `Operations`, `Workflows`. **[target]** areas are re-grouped around the application hierarchy: `Applications`, `Discovery`, `Assistant`, `Channels`, `Knowledge`, `Navigation`, `Tools`/`Gateway`, `Governance`, `Operations`.

`Common/`: `Interfaces/` (service contracts implemented by `Infrastructure`), `Behaviors/` (MediatR pipeline — `LoggingBehavior`, `ValidationBehavior`), `Exceptions/` (`NotFoundException`, `ValidationException`, `UnauthorizedException`), `DiagnosticsConfig.cs` (OpenTelemetry `ActivitySource`).

### R2WAI.Infrastructure

- **AI** (`AI/`): `SemanticKernelService` implements `IAIService`. Provider selection (`AI:Provider`: `openai` | `ollama` | `zai`) built via `Kernel.CreateBuilder().AddOpenAIChatCompletion/AddOpenAIEmbeddingGenerator`, with a static kernel cache (1-hour max age). Plugins (`AI/Plugins/`): `WorkflowPlugin`, `DocumentPlugin`, `RAGPlugin`, `AssistantPlugin` — only attached when the caller passes `enableTools: true` (see AI Integration). `AiFunctionAuditFilter` logs every tool invocation when tools are enabled. `AI/Prompts/SystemPromptTemplates.cs` holds the live prompt templates.
- **Persistence** (`Persistence/`): `ApplicationDbContext` (23 `DbSet<T>`), `Configurations/` (EF Core Fluent API per entity), `Migrations/`, `Repositories/GenericRepository.cs`, `UnitOfWork.cs`. **[target]** `Application`, `Department`, `ApplicationApi`, `ApplicationDiscovery`, `Tool`/`GatewayEndpoint`, `GovernancePolicy`, `ModelRegistry` entities are added.
- **Services** (`Services/`): `AssistantService`, `ChatService`, `ChatbotService`, `WorkflowService`, `ApprovalService`, `DocumentService`, `KnowledgeBaseService`, `EmailService`, `EscalationBackgroundService`, `FileProcessingService`, `CurrentUserService`, `ToolFramework/` (`ITool`, `ToolRegistry`, `HttpTool`, `EmailTool`). **[target]** `ToolFramework` is redesigned as a **Tool/API Gateway** with per-tool risk metadata and enforcement (see Gateway).
- **Storage**: `LocalStorageService` / `MinioStorageService`, switched by `Storage:Provider`.
- **Cache**: `RedisCacheService` / `InMemoryCacheService`, switched by presence of `Cache:Redis:ConnectionString`.
- **Auth**: `PasswordHasher`, `JwtService`, `TotpService` (MFA), `EntraIdAuthService`. **[target]** authorization policy evaluation (`RBAC + ABAC`) moves behind a dedicated policy service so the AI model never decides permissions.
- **VectorStore**: `PgVectorService` implements `IVectorStoreService` — RAG backed by the `pgvector` PostgreSQL extension (see Data Layer).

### R2WAI.Web

Blazor Server, MudBlazor. `Components/Pages/` (~40 routed pages spanning every domain area), `Components/Dialogs/` (create/edit/detail dialogs), `Components/Shared/` (charts, markdown editor/renderer, command palette, voice orb, etc.), `Components/Layout/` (`MainLayout`, `AuthLayout`, `WidgetLayout` — the last used only by the embeddable chatbot widget route). `Services/`: `AuthenticatedHttpClient` (JWT-attached), `ChatSessionService`, `VoiceSession`.

**[target]** the primary navigation is the **Application Studio** screen; most configuration is defaulted and hidden behind an "Advanced Configuration" disclosure.

---

## Target Domain Model

```text
Tenant
│
├── Department
│    │
│    ├── Application
│    │    ├── API
│    │    ├── Assistant
│    │    ├── Knowledge
│    │    ├── Navigation
│    │    ├── Tool
│    │    ├── Workflow
│    │    └── Policy
│    │
│    └── Users
│
├── ModelRegistry
├── GlobalPolicy
└── AuditLog
```

> **Status: implemented.** `Tenant → Department → ConnectedApplication` exists in the domain model (migration `20260810174027_AddDepartmentsAndApplications` and later) with full CRUD (`DepartmentsController`, `ApplicationsController`). The entity is named `ConnectedApplication`, not `Application`, to avoid colliding with the `R2WAI.Application` project namespace (see ADR-0001) — "Application" below refers to this entity. `ApplicationApi`, `ApplicationConfiguration`, `ApplicationVersion`, `NavigationDefinition`, and `GlobalPolicy` are likewise implemented. The one piece of this hierarchy still `[target]`: per-application scoping of `Integration`/`ModelConfiguration` records is partial (Integrations has no `applicationId` filter yet).

### Entity notes

- **Tenant / Department / Application**: the three-level ownership hierarchy. `Application` is the central entity — it represents the external system (Property Tax, Revenue, Agriculture, HR, …) being connected.
- **Application → Assistant**: one assistant per application, exposed through multiple **Channels** (Embedded Web widget, Application, API, future channels). The chatbot is a *channel*, not a separate product entity.
- **Application → API / Tool**: discovered endpoints; tools are generated from APIs (`Swagger → Endpoint → Tool`) rather than hand-authored in a generic tool studio.
- **Knowledge**: RAG content (FAQ, policy, documents) is kept **separate from live API data** — the assistant can combine both, but they are sourced and governed differently.
- **ModelRegistry**: platform-level model governance — super admins approve models; department admins select from the approved set.
- **AuditLog**: every mutation and every tool/gateway invocation is audited.

---

## Target Architecture Areas

### 1. Application Studio — the MAIN product

```text
APPLICATION STUDIO
│
├── Applications
│
└── Application
     ├── Overview
     ├── Discovery
     ├── APIs
     ├── Knowledge
     ├── Navigation
     ├── Assistant
     ├── Tools
     ├── Workflows
     ├── Security
     ├── Policies
     ├── Testing
     ├── Monitoring
     └── Publish
```

The default screen after connecting an application is a **review checklist** (everything auto-configured), not a 13-page wizard:

```text
✓ Connected
✓ API discovered
✓ Knowledge configured
✓ Assistant configured
✓ Security configured
✓ Monitoring enabled

        [ Test Assistant ]
        [ Publish ]
```

Advanced configuration (Model, Prompt, Chunking, Embedding, Tool permissions, API timeout, RAG threshold, Security policy, Workflow settings) is hidden behind **Advanced Configuration** and is editable after publishing.

### 2. Application Discovery Engine

Connect an existing application → automatically understand it → generate configuration → **admin only reviews**.

```text
Application Discovery
├── OpenAPI/Swagger discovery
├── Endpoint discovery
├── Authentication detection
├── API schema analysis
├── Navigation discovery
├── Capability detection
├── Tool suggestion
├── FAQ generation
├── Permission suggestion
└── Assistant configuration
```

### 3. Adaptive Wizard

No mandatory multi-page wizard. Flow:

```text
Connect
   ↓
Auto Discover
   ↓
Auto Configure
   ↓
Review Issues
   ↓
Test
   ↓
Publish
```

All areas remain editable afterward from the Application Studio.

### 4. Tool/API Gateway

The LLM **never** calls government APIs directly. Every tool call passes through a gateway:

```text
User
 ↓
AI Assistant
 ↓
Authorization
 ↓
Policy
 ↓
Tool Gateway
 ↓
Application API
```

Every tool carries:

```text
Application · Endpoint · HTTP Method · Role · Permission
Risk Level · Confirmation Required · Approval Required · Audit Required
```

### 5. RAG vs Live Data

```text
FAQ / Policy / Documents  →  RAG
Current status / transactions / application data  →  API
```

The assistant can combine both sources but they are never treated as the same thing.

### 6. Assistant + Channels

`Chatbot` is removed as a standalone entity and merged into `Assistant → Channels`:

```text
Assistant
   │
   └── Channels
        ├── Embedded Web
        ├── Application
        ├── API
        └── Future channels
```

Floating chatbot architecture — one platform serves every application's widget:

```text
Existing Website
      │
      ▼
R2WAI Floating Widget
      │
      ▼
Application ID
      │
      ▼
R2WAI Gateway
      │
      ▼
Application Assistant
```

### 7. Workflow & Approvals

**Elsa is kept** — behind the R2WAI workflow abstraction (`IWorkflowBridge`). It remains the right engine for workflow, approval, escalation, notification, scheduling, and human-in-the-loop.

### 8. AI Governance

```text
AI Governance
├── Model Registry
├── Approved Models
├── Model Policy
├── Prompt Policy
├── Tool Policy
├── Data Policy
├── Risk Policy
└── Evaluation
```

Super Admin controls approved models; Department Admin selects from permitted models. Model management is **Administration → Model Governance**, not a user-facing studio.

### 9. Security: RBAC + ABAC

Role-based access is upgraded with attribute-based access. Decisions combine:

```text
Role + Department + Application + Jurisdiction
+ Record ownership + Action + Policy
```

The AI model **never determines these permissions itself** — enforcement is purely in the policy/gateway layer.

### 10. Configuration Inheritance

```text
GLOBAL POLICY
      ↓
DEPARTMENT POLICY
      ↓
APPLICATION POLICY
      ↓
ASSISTANT POLICY
      ↓
USER CONTEXT
```

Example: Super Admin disables external AI providers → Department allows only approved model X → Application enables RAG → Assistant enables tool Y → Officer may execute Y.

---

## Role Hierarchy

```text
SUPER ADMIN
     │
     ├── Global Governance
     ├── Model Governance
     ├── Security Governance
     └── Departments
             │
             ▼
       DEPARTMENT ADMIN
             │
             ├── Applications
             ├── Assistants
             ├── Knowledge
             ├── APIs
             ├── Tools
             ├── Workflows
             └── Policies
                     │
              ┌──────┴──────┐
              ▼             ▼
           OFFICER         USER
              │             │
              └──────┬──────┘
                     ▼
                  PUBLIC
```

**Public is not a role in the same hierarchy** — it is an anonymous access context.

> **Status: implemented as a UI-layer persona system, not new backend roles — since revised from 5 tiers to 3.** `R2WAI.Web/Authentication/RolePersona.cs` originally mapped the *existing* seeded RBAC roles onto the five navigation personas shown in the diagram above (`SystemAdmin`→Super Admin, `Admin`→Department Admin, `WorkflowManager`/`Editor`/`Contributor`/`UserManager`→Officer, plain `User`→Citizen, unauthenticated→Public). A later redesign pass (2026-08-21/22) collapsed this to the platform's final role model — **SUPER ADMIN / ADMIN / USER** (+ `Public` for anonymous access only): `SystemAdmin`→**Super Admin**, `Admin` and the former Officer roles (`WorkflowManager`/`Editor`/`Contributor`/`UserManager`) all → **Admin**, plain `User`→**User**. `MainLayout.razor`'s primary navigation was rewritten to match, with Department/Application/Officer/Department-Admin concepts removed from primary nav entirely (see "Primary UI Areas" below). This remains deliberately additive: zero changes to `Program.cs` policies, `[Authorize(Roles=...)]` attributes, or the `AuthorizationBehavior` MediatR pipeline — the actual authorization boundary is unchanged, only which menu items a user sees and how the 6 real DB roles bucket into 3 nav-facing ones. The role *names* in the DB are still `Admin`/`WorkflowManager`/etc. (`SystemAdmin` remains an aspirational, never-seeded claim value — see Adoption Status). Renaming the DB roles themselves, or building true ABAC per the target below, remains `[target]`.

### Access scopes

| Context | Scope |
|---|---|
| **Public** | Public FAQ · Public RAG · Public Navigation · Public APIs |
| **User** | Own information · Own applications · Own documents · Approved transactions |
| **Officer** | Department · Jurisdiction · Assigned cases · Internal knowledge · Authorized workflows |

---

## Component Action Register

| Component | Action |
| --- | --- |
| Tenant | **KEEP** |
| Department | **ADD / MODIFY** |
| Application | **ADD — CORE** |
| Assistant | **KEEP / MODIFY** |
| Chatbot entity | **MERGE INTO ASSISTANT CHANNELS** |
| Chatbot Studio | **REMOVE** |
| Agent Studio | **REMOVE / MERGE** |
| Knowledge/RAG | **KEEP / COMPLETE** |
| Model Studio | **REMOVE FROM PRIMARY UX** |
| Model Registry | **KEEP** |
| Integration Studio | **REMOVE FROM PRIMARY UX** |
| Application APIs | **ADD / CORE** |
| Tool Framework | **KEEP, redesign as Tool Gateway** |
| Navigation | **MOVE INTO APPLICATION** |
| Workflow Studio | **KEEP** |
| Elsa | **KEEP** |
| Approval | **KEEP / COMPLETE** |
| Operations Center | **KEEP** |
| Media Studios | **REMOVE FROM GOVERNMENT CORE** |
| RBAC | **KEEP / HARDEN** |
| ABAC | **ADD** |
| Audit | **KEEP / EXPAND** |
| AI Governance | **ADD** |
| Application Discovery | **ADD — CORE** |
| Low-code Wizard | **ADD — CORE** |
| Floating Widget | **KEEP / MODIFY** |
| PostgreSQL | **KEEP** |
| pgvector | **KEEP** |
| Qdrant | **REMOVE** |
| Redis | **OPTIONAL / LATER** |
| MinIO | **OPTIONAL / LATER** |
| Docker | **KEEP** |
| Kubernetes | **LATER** |

---

## Primary UI Areas

The many studios collapse to four primary areas plus Administration:

```text
┌────────────────────────────────────┐
│          R2WAI PLATFORM            │
├────────────────────────────────────┤
│  1. APPLICATION STUDIO             │
│  2. ASSISTANT STUDIO               │
│  3. WORKFLOW STUDIO                │
│  4. OPERATIONS CENTER              │
└────────────────────────────────────┘
Administration = platform section, not a studio.
```

> **Status: implemented, with a UI-only rename** — Each `ConnectedApplication`'s workspace (`/applications/{id}`) has dedicated `Overview / AI Assistant / Automations / Test / Publish / Monitor / Settings` tabs (`ApplicationWorkspace.razor`), the "Application Studio" collapsed into one page per application. "Workflow Studio" is labeled **Automations** everywhere a user sees it; the route (`/workflow-studio`), the `Workflow` entity, and the API stay unchanged.
>
> **Superseded for primary navigation (2026-08-21/22).** A later redesign pass made **Application Studio itself** (and Department) no longer part of primary navigation — the brief driving that pass explicitly wants Department/Application/technical schema concepts hidden from everyday users, with primary nav flattened to `Dashboard, AI Assistants, Automations, Knowledge, Integrations, Tools & APIs, Test & Playground, Publish, Monitor, Users & Roles, Settings` (varying by the 3 roles — see Role Hierarchy above). `ApplicationWorkspace.razor` and `Applications.razor` are unchanged and still fully functional at their existing routes; they're just no longer linked from `MainLayout.razor`'s nav for any role. This is a real pivot from the "Application-centric platform" framing at the top of this document, not an extension of it — the brief's own words: "keep the reference architecture internally, but hide technical complexity from normal users." Six previously-`[target]` shared-platform components were also built in this pass, closing real gaps rather than just the UI rename above — see the new rows in Adoption Status below: **AI Model Gateway**, **Tool/Integration Registry bridge**, **Policy Engine**, **Prompt Management**, **Context & Memory**, **Agent Runtime**.

---

## Current Implementation Notes

### AI Integration

`SemanticKernelService.GetOrCreateKernel(enableTools)` is the security boundary for tool-calling: the base kernel always has `ConversationSummaryPlugin` and `TimePlugin`; the mutating plugins (`WorkflowPlugin` — `start_workflow`, `submit_approval_request`, `notify_approver`, etc.) are only attached when `enableTools: true`. Authenticated chat surfaces (`ChatHub`, `AssistantsController`) pass `true`; the public, anonymous chatbot widget (`ChatbotsController`) hard-codes `false`, so an anonymous website visitor can never trigger a workflow or approval action through the chatbot. **[target]** the `enableTools` boundary is superseded by the explicit **Tool/API Gateway**, which enforces per-tool role/permission/risk policies independent of the kernel.

### Data Layer

PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` (retry-on-failure). Vector search uses the **pgvector** extension through `PgVectorService` (`VectorStore:Provider=pgvector`, `VectorStore:VectorSize=1536`) — there is no separate vector database (Qdrant is removed from the target stack). Elsa's own persistence module points at the same connection string and PostgreSQL instance.

### Auth

JWT Bearer is the primary scheme (`Authentication:Jwt:*`), with SignalR hubs accepting the token via `?access_token=` query string. Also present: Azure Entra ID SSO (`EntraIdAuthService`), TOTP-based MFA (`TotpService`), and a separate `X-API-Key` header scheme (`ApiKeyAuthenticationMiddleware`) for programmatic API access, independent of JWT.

### Deployment

Docker Compose is the deployment target (`docker/docker-compose.yml`: `r2wai-web`, `r2wai-api`, `postgres` on the `pgvector/pgvector:pg16` image). Kubernetes manifests were scoped out of the MVP in favor of Docker Compose (see `docs/implementation/MVP-IMPLEMENTATION-PLAN.md`) and are not present in this repo. Kubernetes and the optional Redis/MinIO components are scheduled **later**, not for the initial government pilot.

---

## Adoption Status

| Area | Status | Notes |
| --- | --- | --- |
| Clean Architecture / CQRS / MediatR | **Implemented** | 4-layer solution, MediatR pipeline behaviors |
| Tenant isolation (EF query filters) | **Implemented** | reflective `ApplyTenantFilter` / `ApplySoftDeleteFilter` |
| RBAC | **Implemented, needs hardening** | authorization policies exist; ABAC is net-new |
| Knowledge / RAG (pgvector) | **Implemented** | file-upload, URL, and text source paths all fetch/chunk/embed/upsert into pgvector (`KnowledgeBaseService.AddSourceAsync`) — verified 2026-08-21, this line previously said URL/text indexing was a gap; that appears to have been closed since without this doc being updated |
| Workflows + Approvals (Elsa) | **Implemented, incomplete** | step chaining, visual designer, approval UI need completion |
| Audit | **Implemented, needs expansion** | auto-populated on mutation; gateway invocations must be added |
| Application (central entity) | **Implemented** | `ConnectedApplication`, with APIs, config, versioning |
| Department | **Implemented** | first-class entity, `DepartmentsController` |
| Role-based navigation | **Implemented, UI-only — revised to 3 roles** | `RolePersona` now maps the 6 DB roles to SUPER ADMIN/ADMIN/USER (+ Public); superseded the earlier 5-persona mapping. No new DB roles, no ABAC |
| Role assignment (assign a role to a user) | **Implemented** | previously missing entirely — no UI or API path existed to grant a role after user creation |
| Application Discovery Engine | **[target]** | net-new |
| Tool/API Gateway + ABAC + Policies | **Partially implemented** | see Tool/Integration Registry bridge and Policy Engine rows below — the dynamic-callable bridge and an optional risk-ceiling policy exist; full ABAC and a rules-engine reading `GlobalPolicy` generically remain `[target]` |
| AI Governance (Model Registry) | **Partially implemented** | see AI Model Gateway row below — provider selection is now a real abstraction; a full model-approval workflow (Super Admin approves, Department Admin selects) remains `[target]` |
| Assistant Channels (Chatbot merge) | **Implemented** | `Assistants.razor`'s Channels tab already hosts the per-assistant "Website" chatbot channel (`ChatbotChannel`); `Chatbots.razor` itself is unlinked from primary nav (unchanged route, still functional) |
| Adaptive wizard / default-hidden config | **Implemented for Automations** | 4-step wizard + Advanced-as-opt-in; AI Assistant/Knowledge/Integrations pages already matched this pattern before this work |
| Application Studio UI | **Implemented as per-application workspace tabs, unlinked from primary nav** | `ApplicationWorkspace.razor` still exists and works, but is no longer reachable via `MainLayout.razor` for any role — see "Primary UI Areas" above |
| AI Model Gateway | **Implemented** | `IModelGateway`/`IModelProvider` (`src/R2WAI.Infrastructure/AI/ModelGateway/`) — provider selection (openai/ollama/zai) extracted from `SemanticKernelService` into an injectable seam; behavior-preserving refactor, unit-tested per provider |
| Tool/Integration Registry bridge | **Implemented** | `DynamicToolFunctionFactory`/`DynamicToolExecutor` (`src/R2WAI.Infrastructure/AI/DynamicTools/`) turn a tenant's `ToolDefinition` rows — both `ApplicationApi`-linked and the direct-`EndpointUrl` shape `Integrations.razor` actually creates — into real SK-callable functions, dispatched through `HttpTool`. Real auth support for Bearer/Basic (values already stored in `Configuration`); `ApplicationApi.CredentialRef` and `ApiKey` scheme remain unresolved/unsupported by design (no credential vault yet) |
| Policy Engine | **Partially implemented** | the "ToolExecution" `GlobalPolicy` can optionally carry a structured `{"maxRiskLevel":"..."}` payload, enforced by `AiFunctionAuditFilter` as an additive tightening layer (never loosens the pre-existing role/approval checks). Other `GlobalPolicy` types (`AiUsage`, `DataRetention`, `Pii`, `Approval`, `Knowledge`) remain prose-only/unenforced; a per-tenant model allow-list was scoped out — the SK kernel cache is process-wide, not per-tenant, so that needs deeper Model Gateway work first |
| Prompt Management | **Implemented** | versioned `PromptTemplate` entity (tenant-scoped, `Supersede()` on edit) + `IPromptTemplateService`, falling back to the static `SystemPromptTemplates` defaults; wired into every live chat entry point that resolves a system prompt — `AssistantService.ChatWithAssistantAsync`, `ChatHub.StreamChat`, `AssistantsController.StreamChat`/`Chat` (via `ChatWithAssistantCommandHandler`), and the Instructions tab's Load Template menu. All previously fell back to a generic hardcoded string instead of the per-type template when `AssistantDefinition.SystemPrompt` was null — fixed 2026-08-22 |
| Context & Memory | **Implemented, off by default** | `ConversationMemoryService`/`ConversationContextBuilder` add a short-term/long-term split (recent turns verbatim, older turns summarized via the existing `IAIService.SummarizeTextAsync`) behind `AI:ContextMemory:SummarizationEnabled` (unset = old fixed-window-and-join behavior, byte-identical). No live-DB regression test exists yet — the documented prerequisite before defaulting it on |
| Agent Runtime | **Implemented** | `IAgentRuntime`/`AgentRuntime` (`src/R2WAI.Infrastructure/AI/AgentRuntime.cs`) — a thin facade over `IAIService` that always enables tools, giving the "tool-enabled agent" path a name distinct from `IAIService`'s general (and, for the anonymous chatbot widget, deliberately tools-off) methods. `ChatService.SendMessageAsync` is the first consumer |

### P0 technical gaps to close before building new features

From the project audit, in priority order:

1. Database migrations for the new entity model
2. Real frontend ↔ backend integration (remove mock-data paths)
3. ~~Real KB indexing / embeddings (close the text/URL source gap)~~ — closed; see Adoption Status above
4. Workflow step chaining
5. Real approval UI
6. Tenant isolation verification across new entities
7. RBAC enforcement review + ABAC introduction
8. API integration (Application → API → Tool pipeline)
9. Security hardening (concurrency tokens, gateway controls)
10. Production configuration validation

---

## Notes on documents in this repo

Some files under `docs/` (roadmaps, implementation plans, runbooks) are point-in-time planning or audit snapshots rather than living documentation — check the date in each file's own header before relying on specifics. This document and `README.md` are the two documents intended to be kept current with the codebase. `ROADMAP.md` tracks the migration to the target architecture above; [docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md](docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md) is the code-ready implementation plan (entities, gateway spec, risk model, phases, acceptance criteria).
