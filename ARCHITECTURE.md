# R2WAI Architecture

> System architecture and design decisions, kept in sync with the codebase (not a roadmap or audit snapshot).

---

## System Overview

R2WAI is a self-hosted, multi-tenant platform combining AI-powered assistants, RAG knowledge bases, enterprise chatbots, and Elsa-backed workflow/approval automation. The backend follows **Clean Architecture**; the frontend is **Blazor Server**.

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

- **Controllers** (`Controllers/`): `Admin`, `ApiKeys`, `Approvals`, `Assistants`, `Auth`, `Chat`, `Chatbots`, `Documents`, `Integrations`, `KnowledgeBases`, `Operations`, `Schedules`, `Webhooks`, `Workflows`.
- **Hubs** (`Hubs/`): `ChatHub` (`/hubs/chat`), `StatusHub` (`/hubs/status`), `NotificationHub` (`/hubs/notification`) — all `[Authorize]`, token accepted via `?access_token=` query string for SignalR clients.
- **Workflows** (`Workflows/`): Elsa custom activities — `ApprovalStepActivity`, `InvokeSemanticKernelActivity`, `TransformStepActivity`, `StepActivityFactory`, `StepStatusNotificationHandler` — bridging the app's own `Workflow`/`WorkflowInstance` model onto the Elsa runtime via `IWorkflowBridge`.
- **Middleware** (`Middleware/`): correlation ID, exception handling, request logging, rate limiting, security headers, tenant resolution, API-key authentication.
- **Program.cs**: composition root. Serilog logging with a sensitive-data enricher; Elsa workflow management + runtime registered against the same PostgreSQL connection string as the app's own `ApplicationDbContext`; JWT Bearer auth; authorization policies (`AdminOnly`, `TenantAccess`, `CanManageUsers`, `CanManageDocuments`, `CanManageWorkflows`); CORS allow-list; OpenTelemetry tracing/metrics; Swagger (Dev/Staging only); health checks at `/health`, `/health/ready`, `/health/startup`; a `ValidateProductionConfig` guard that refuses to start in Production if secrets are still `CHANGE_ME` placeholders.

### R2WAI.Application

CQRS via MediatR under `Features/{Area}/{Commands,Queries,DTOs}`. Areas: `Admin`, `Assistants`, `Chat`, `Chatbots`, `Documents`, `Integrations`, `KnowledgeBases`, `Operations`, `Workflows`.

`Common/`: `Interfaces/` (service contracts implemented by `Infrastructure`), `Behaviors/` (MediatR pipeline — `LoggingBehavior`, `ValidationBehavior`), `Exceptions/` (`NotFoundException`, `ValidationException`, `UnauthorizedException`), `DiagnosticsConfig.cs` (OpenTelemetry `ActivitySource`).

### R2WAI.Infrastructure

- **AI** (`AI/`): `SemanticKernelService` implements `IAIService`. Provider selection (`AI:Provider`: `openai` | `ollama` | `zai`) built via `Kernel.CreateBuilder().AddOpenAIChatCompletion/AddOpenAIEmbeddingGenerator`, with a static kernel cache (1-hour max age). Plugins (`AI/Plugins/`): `WorkflowPlugin`, `DocumentPlugin`, `RAGPlugin`, `AssistantPlugin` — only attached when the caller passes `enableTools: true` (see Security below). `AiFunctionAuditFilter` logs every tool invocation when tools are enabled. `AI/Prompts/SystemPromptTemplates.cs` holds the live prompt templates.
- **Persistence** (`Persistence/`): `ApplicationDbContext` (23 `DbSet<T>`), `Configurations/` (EF Core Fluent API per entity), `Migrations/`, `Repositories/GenericRepository.cs`, `UnitOfWork.cs`.
- **Services** (`Services/`): `AssistantService`, `ChatService`, `ChatbotService`, `WorkflowService`, `ApprovalService`, `DocumentService`, `KnowledgeBaseService`, `EmailService`, `EscalationBackgroundService`, `FileProcessingService`, `CurrentUserService`, `ToolFramework/` (`ITool`, `ToolRegistry`, `HttpTool`, `EmailTool`).
- **Storage**: `LocalStorageService` / `MinioStorageService`, switched by `Storage:Provider`.
- **Cache**: `RedisCacheService` / `InMemoryCacheService`, switched by presence of `Cache:Redis:ConnectionString`.
- **Auth**: `PasswordHasher`, `JwtService`, `TotpService` (MFA), `EntraIdAuthService`.
- **VectorStore**: `PgVectorService` implements `IVectorStoreService` — RAG backed by the `pgvector` PostgreSQL extension (see Data Layer).

### R2WAI.Web

Blazor Server, MudBlazor. `Components/Pages/` (~40 routed pages spanning every domain area), `Components/Dialogs/` (create/edit/detail dialogs), `Components/Shared/` (charts, markdown editor/renderer, command palette, voice orb, etc.), `Components/Layout/` (`MainLayout`, `AuthLayout`, `WidgetLayout` — the last used only by the embeddable chatbot widget route). `Services/`: `AuthenticatedHttpClient` (JWT-attached), `ChatSessionService`, `VoiceSession`.

## Domain Model

Multi-tenant: every tenant-scoped entity carries `TenantId`, enforced via EF Core global query filters applied reflectively in `ApplicationDbContext.OnModelCreating` (`ApplyTenantFilter` / `ApplySoftDeleteFilter`). All entities inherit `BaseEntity<Guid>` (`Id`, `CreatedAt`/`ModifiedAt`, `IsDeleted`, `DomainEvents`).

Core entities: **Tenant, User, Role, ApiKey** · **Conversation → Message → MessageAttachment** · **Document** · **KnowledgeBase → KnowledgeBaseSource** · **Chatbot** (multi-channel, webhook key, embed script, voice-enabled) · **AssistantDefinition** (draft/published/archived, versioned) · **Workflow → WorkflowInstance → WorkflowStepExecution**, **WorkflowSchedule** · **ApprovalPolicy → ApprovalRequest** (multi-level, SLA/escalation) · **ToolDefinition** · **ModelConfiguration** · **WebhookEndpoint** · **AuditLog** (auto-populated on every mutation in `SaveChangesAsync`).

Domain events (`MessageCreatedEvent`, `DocumentUploadedEvent`, `DocumentProcessedEvent`) are raised via `entity.AddDomainEvent(...)` and dispatched through MediatR after `SaveChangesAsync`.

## AI Integration

`SemanticKernelService.GetOrCreateKernel(enableTools)` is the security boundary for tool-calling: the base kernel always has `ConversationSummaryPlugin` and `TimePlugin`; the mutating plugins (`WorkflowPlugin` — `start_workflow`, `submit_approval_request`, `notify_approver`, etc.) are only attached when `enableTools: true`. Authenticated chat surfaces (`ChatHub`, `AssistantsController`) pass `true`; the public, anonymous chatbot widget (`ChatbotsController`) hard-codes `false`, so an anonymous website visitor can never trigger a workflow or approval action through the chatbot.

## Data Layer

PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` (retry-on-failure). Vector search uses the **pgvector** extension through `PgVectorService` (`VectorStore:Provider=pgvector`, `VectorStore:VectorSize=1536`) — there is no separate vector database. Elsa's own persistence module points at the same connection string and PostgreSQL instance.

## Auth

JWT Bearer is the primary scheme (`Authentication:Jwt:*`), with SignalR hubs accepting the token via `?access_token=` query string. Also present: Azure Entra ID SSO (`EntraIdAuthService`), TOTP-based MFA (`TotpService`), and a separate `X-API-Key` header scheme (`ApiKeyAuthenticationMiddleware`) for programmatic API access, independent of JWT.

## Deployment

Docker Compose is the deployment target (`docker/docker-compose.yml`: `r2wai-web`, `r2wai-api`, `postgres` on the `pgvector/pgvector:pg16` image). Kubernetes manifests were scoped out of the MVP in favor of Docker Compose (see `docs/implementation/MVP-IMPLEMENTATION-PLAN.md`) and are not present in this repo.

## Notes on documents in this repo

Some files under `docs/` (roadmaps, implementation plans, runbooks) are point-in-time planning or audit snapshots rather than living documentation — check the date in each file's own header before relying on specifics. This document and `README.md` are the two documents intended to be kept current with the codebase.
