# R2WAI MVP Implementation Plan

> **Author:** Technical Lead
> **Status:** Final
> **Date:** 2026-06-17
> **Stack:** Blazor Server, MudBlazor, ASP.NET Core 10, Clean Architecture, Semantic Kernel, Elsa Server, PostgreSQL, Docker, JWT, Microsoft Entra ID

---

## 1. MVP Definition

### Included

| Area | Scope |
|------|-------|
| **Studios** | AI Assistant Studio, Workflow Studio, Integrations Studio, Operations Center, Settings |
| **Authentication** | JWT login/password + Microsoft Entra ID token exchange |
| **AI** | Semantic Kernel integration with OpenAI/Azure OpenAI, RAG with Qdrant vector store |
| **Workflows** | Elsa Server runtime, approval steps, human-in-the-loop, escalation |
| **Documents** | Upload, process, summarize, extract, compare, RAG indexing |
| **Knowledge Bases** | CRUD, source management, vector search |
| **Chat** | Conversations, messages, streaming, attachments |
| **Assistants** | Domain-specific assistants (HR, IT, Procurement, Finance, Legal), chat with RAG context |
| **Chatbots** | Embeddable chatbot definitions (merged into AI Assistant Studio scope) |
| **Operations** | Workflow instance tracking, audit logs, metrics dashboard |
| **Admin** | User CRUD, role CRUD, model configuration, audit logs, analytics |
| **Infrastructure** | Docker Compose for local dev, PostgreSQL, Redis, Qdrant, Serilog logging, OpenTelemetry, health checks |
| **CI/CD** | GitHub Actions build/test/publish |
| **Real-time** | SignalR hubs for chat, status, notifications |
| **Security** | JWT auth, tenant isolation, role-based authorization, security headers, rate limiting, audit logging |

### Excluded

| Feature | Reason |
|---------|--------|
| Kubernetes manifests (k8s/) | Scoped out per architecture decision; Docker Compose is production target for MVP |
| MinIO storage | Scoped out per architecture decision; local storage used for MVP |
| Marketplace / Prompt Marketplace / Workflow Marketplace | Scoped out per architecture decision |
| Multi-Agent Framework | Scoped out per architecture decision |
| React frontend | Scoped out per architecture decision |
| Multi-factor authentication | Post-MVP enhancement |
| SSO beyond Entra ID | Post-MVP enhancement |
| Advanced analytics / reporting | Post-MVP enhancement |
| Mobile app | Post-MVP |
| Webhook integrations | Post-MVP |
| Tenant self-service onboarding | Post-MVP |

---

## 2. Feature Completion Matrix

### AI Assistant Studio

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| Assistant CRUD (API) | 90% | Publish/unpublish workflow, versioning | Migration for publish state | P1 |
| Assistant CRUD (UI) | 30% | Create/Edit dialogs, chat dialog, search, pagination | API complete | P0 |
| Assistant Chat | 70% | Streaming UI integration, conversation persistence | SignalR chat hub | P0 |
| Model Configuration | 80% | API complete; UI needs create/edit dialogs | None | P1 |
| Knowledge Base linking | 70% | API exists; UI picker needed | Knowledge Bases complete | P1 |
| Tool configuration | 40% | Tool selection UI, plugin registration | Integrations API | P2 |
| Publishing workflow | 0% | Draft → Published state machine, approval gate | Approval system | P1 |

### Workflow Studio

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| Workflow CRUD (API) | 80% | Step definitions via Elsa Studio integration | Elsa Studio setup | P0 |
| Workflow CRUD (UI) | 30% | Create/Edit dialogs, Elsa Studio embed | API complete | P0 |
| Elsa Studio embed | 0% | Embed Elsa Studio in Workflow Studio page | Elsa Server configured | P0 |
| Workflow execution | 50% | WorkflowBridge maps steps; Elsa runtime starts | Elsa Server | P0 |
| Step types | 60% | Approval, AI/Generate, WriteLine implemented | None | P1 |
| Trigger configuration | 20% | Trigger field exists; no trigger evaluation | None | P2 |

### Integrations Studio

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| Integration CRUD (API) | 80% | Uses ToolDefinition directly; no MediatR/CQRS | None | P1 |
| Integration CRUD (UI) | 30% | Create/Edit dialogs, search, pagination | API complete | P0 |
| Tool execution | 40% | HttpTool, EmailTool exist; no UI test/execute | API complete | P2 |
| Integration categories | 20% | Type filtering exists; no category taxonomy | None | P2 |

### Operations Center

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| Workflow Instances view | 50% | API exists; UI lacks detail view, filtering, cancellation | Workflow execution | P0 |
| Audit Logs view | 50% | API exists; UI lacks detail view, filtering, export | Audit middleware | P0 |
| Metrics dashboard | 40% | Basic metrics API; UI needs charts, refresh | None | P1 |
| Health checks | 80% | DB, Redis, Memory checks; UI health page needed | None | P1 |
| Approval requests view | 20% | No dedicated approval UI page | Approval API | P0 |

### Settings

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| User Management (API) | 90% | Full CRUD with MediatR; password reset missing | None | P1 |
| User Management (UI) | 40% | List exists; no create/edit dialogs, role assignment | API complete | P0 |
| Role Management (API) | 90% | Full CRUD with MediatR | None | P1 |
| Role Management (UI) | 40% | List exists; no create/edit dialogs, permission assignment | API complete | P0 |
| Model Configuration (API) | 90% | Full CRUD with MediatR | None | P1 |
| Model Configuration (UI) | 40% | List exists; no create/edit dialogs | API complete | P0 |
| System Settings (API) | 50% | Get/Update settings; no settings schema | None | P2 |
| System Settings (UI) | 10% | Placeholder text only | API complete | P2 |

### Cross-Cutting

| Feature | Complete | Missing Work | Dependencies | Priority |
|---------|----------|-------------|--------------|----------|
| Authentication | 80% | JWT login, Entra ID exchange, refresh; auto-logout UI | None | P0 |
| Authorization | 70% | Role policies defined; permission-based checks partial | None | P1 |
| Tenant isolation | 70% | TenantResolutionMiddleware, TenantAuthorizationFilter | None | P1 |
| Audit logging | 60% | AuditLog entity; no interceptor/auto-logging | None | P1 |
| Validation | 60% | FluentValidation exists; some commands lack validation | None | P1 |
| Error handling | 70% | ExceptionHandlingMiddleware; no structured error responses | None | P1 |
| Logging | 70% | Serilog configured; request logging middleware exists | None | P1 |
| OpenTelemetry | 60% | Tracing/Metrics configured; console exporter default | None | P2 |
| CORS | 80% | Configured with allowed origins | None | P1 |
| Rate limiting | 50% | Middleware exists; no configuration | None | P2 |

---

## 3. Development Backlog

### Epic 1: Frontend Completion

#### Feature 1.1: Assistant Studio UI
- **Task 1.1.1** — Implement Create Assistant dialog (name, description, type, model, KB, system prompt, tools)
  - Subtask: Build MudBlazor dialog form
  - Subtask: Wire API POST /api/v1/assistants
  - Subtask: Add validation feedback
- **Task 1.1.2** — Implement Edit Assistant dialog
  - Subtask: Populate form from existing data
  - Subtask: Wire API PUT /api/v1/assistants/{id}
- **Task 1.1.3** — Implement Chat dialog
  - Subtask: Build chat UI with message history
  - Subtask: Wire API POST /api/v1/assistants/{id}/chat
  - Subtask: Add streaming via SignalR
- **Task 1.1.4** — Add search, pagination, filtering to list
- **Task 1.1.5** — Add publish/unpublish toggle

#### Feature 1.2: Workflow Studio UI
- **Task 1.2.1** — Embed Elsa Studio in iframe or Blazor component
  - Subtask: Configure Elsa Studio route
  - Subtask: Handle authentication token passing
- **Task 1.2.2** — Implement Create Workflow dialog
- **Task 1.2.3** — Implement Edit Workflow dialog
- **Task 1.2.4** — Add search, pagination, filtering
- **Task 1.2.5** — Implement execute button with confirmation

#### Feature 1.3: Integrations Studio UI
- **Task 1.3.1** — Implement Create Integration dialog
  - Subtask: Build form with type selection dropdown
  - Subtask: Dynamic fields based on tool type
  - Subtask: Wire API POST /api/v1/integrations
- **Task 1.3.2** — Implement Edit Integration dialog
- **Task 1.3.3** — Add search, pagination, filtering

#### Feature 1.4: Operations Center UI
- **Task 1.4.1** — Build Approval Requests tab
  - Subtask: List pending approvals
  - Subtask: Approve/Reject buttons with comment modal
  - Subtask: Wire API POST /api/v1/approvals/{id}/approve|reject
  - Subtask: Real-time update via SignalR
- **Task 1.4.2** — Enhance Workflow Instances tab
  - Subtask: Detail view (steps, data, timeline)
  - Subtask: Cancel instance button
  - Subtask: Advanced filtering
- **Task 1.4.3** — Enhance Audit Logs tab
  - Subtask: Detail view (old/new values diff)
  - Subtask: Filter by action, entity, user, date range
  - Subtask: Export to CSV
- **Task 1.4.4** — Enhance Metrics tab
  - Subtask: Auto-refresh interval
  - Subtask: Add charts (workflow success rate, approvals pending, etc.)

#### Feature 1.5: Settings UI Completion
- **Task 1.5.1** — Implement Create/Edit User dialog
  - Subtask: Form with email, name, role assignment
  - Subtask: Wire API POST/PUT /api/v1/admin/users
- **Task 1.5.2** — Implement Create/Edit Role dialog
  - Subtask: Form with name, description, permission checkboxes
  - Subtask: Wire API POST/PUT /api/v1/admin/roles
- **Task 1.5.3** — Implement Create/Edit Model dialog
  - Subtask: Form with provider, model ID, API key, max tokens, temperature
  - Subtask: Wire API POST/PUT /api/v1/admin/models

#### Feature 1.6: Login & Auth UI
- **Task 1.6.1** — Enhance Login page
  - Subtask: Add error state display
  - Subtask: Add loading state
  - Subtask: Add Entra ID login button
- **Task 1.6.2** — Implement auto-redirect to login on 401
- **Task 1.6.3** — Implement token refresh logic in AuthDelegatingHandler

### Epic 2: Backend Completion

#### Feature 2.1: Integrations Application Layer
- **Task 2.1.1** — Create Features/Integrations module
  - Subtask: CreateIntegrationCommand + handler + validator
  - Subtask: UpdateIntegrationCommand + handler + validator
  - Subtask: ToggleIntegrationCommand + handler
  - Subtask: DeleteIntegrationCommand + handler + validator
  - Subtask: GetIntegrationsQuery + handler
  - Subtask: GetIntegrationByIdQuery + handler
  - Subtask: IntegrationDto, IntegrationProfile
- **Task 2.1.2** — Refactor IntegrationsController to use MediatR

#### Feature 2.2: Application Layer Gaps
- **Task 2.2.1** — Add missing handlers for all commands/queries (verify all IRequest/IRequestHandler pairs complete)
- **Task 2.2.2** — Add validation for all commands (AuditLog queries, Settings commands, etc.)
- **Task 2.2.3** — Add missing DTO properties (AssistantDto needs publish status, etc.)

#### Feature 2.3: Workflow Execution Engine
- **Task 2.3.1** — Complete WorkflowBridge step mapping
  - Subtask: Add HttpRequestStep activity integration
  - Subtask: Add EmailStep activity integration
  - Subtask: Add ConditionalBranch step
  - Subtask: Add ParallelExecution step
- **Task 2.3.2** — Implement workflow instance state machine
  - Subtask: Draft → Active → Running → Completed/Failed
  - Subtask: Cancellation support
  - Subtask: Retry failed steps
- **Task 2.3.3** — Connect Elsa workflow completion to R2WAI instance status update
- **Task 2.3.4** — Implement Elsa workflow definition sync (bidirectional)

#### Feature 2.4: Assistant Publishing
- **Task 2.4.1** — Add PublishStatus field to AssistantDefinition entity
- **Task 2.4.2** — Add migration for PublishStatus
- **Task 2.4.3** — Implement publish/unpublish API endpoints
- **Task 2.4.4** — Add publish approval workflow integration

#### Feature 2.5: Audit System Enhancement
- **Task 2.5.1** — Implement EF Core SaveChangesInterceptor for auto-audit logging
- **Task 2.5.2** — Add audit for all major entity changes (create, update, delete)
- **Task 2.5.3** — Add user context to audit logs (IP, user agent, etc.)
- **Task 2.5.4** — Implement audit log cleanup/purge background job

#### Feature 2.6: Notification System
- **Task 2.6.1** — Implement notification creation on approval requests
- **Task 2.6.2** — Push notifications via SignalR
- **Task 2.6.3** — Add notification badge to UI

### Epic 3: Elsa Runtime Integration

#### Feature 3.1: Elsa Server Setup
- **Task 3.1.1** — Configure Elsa Server with PostgreSQL persistence
- **Task 3.1.2** — Configure Elsa Studio embedding
- **Task 3.1.3** — Configure Elsa identity/authentication integration
- **Task 3.1.4** — Verify Elsa migrations run on startup

#### Feature 3.2: Approval Workflow
- **Task 3.2.1** — Complete ApprovalStepActivity implementation
  - Subtask: Create approval request in R2WAI DB
  - Subtask: Wait for approval via bookmark
  - Subtask: Escalation timeout handling
- **Task 3.2.2** — Implement approval notification triggers
- **Task 3.2.3** — Wire approval decision back to Elsa workflow resume

#### Feature 3.3: Workflow Execution
- **Task 3.3.1** — Implement workflow execution telemetry
- **Task 3.3.2** — Handle workflow failure and retry
- **Task 3.3.3** — Implement workflow cancellation from UI

### Epic 4: Knowledge & AI

#### Feature 4.1: Knowledge Indexing Pipeline
- **Task 4.1.1** — Implement document chunking strategy (size, overlap, boundaries)
- **Task 4.1.2** — Implement background indexing with progress tracking
- **Task 4.1.3** — Implement incremental indexing (only new/changed docs)
- **Task 4.1.4** — Implement indexing status reporting to UI

#### Feature 4.2: AI Enhancement
- **Task 4.2.1** — Add token usage tracking
- **Task 4.2.2** — Implement model fallback (primary → secondary)
- **Task 4.2.3** — Add prompt template management
- **Task 4.2.4** — Implement conversation context window management

### Epic 5: Security

#### Feature 5.1: Authentication
- **Task 5.1.1** — Implement token refresh with sliding expiration
- **Task 5.1.2** — Add refresh token rotation
- **Task 5.1.3** — Implement session management (force logout)
- **Task 5.1.4** — Complete Entra ID token validation with certificate verification

#### Feature 5.2: Authorization
- **Task 5.2.1** — Implement permission-based authorization (not just role-based)
- **Task 5.2.2** — Add resource-level authorization checks
- **Task 5.2.3** — Complete tenant-scoped data access verification

#### Feature 5.3: Audit & Compliance
- **Task 5.3.1** — Complete audit trail for all sensitive operations
- **Task 5.3.2** — Add audit viewer with search/filter/export
- **Task 5.3.3** — Implement data retention policies

### Epic 6: Production Readiness

#### Feature 6.1: Deployment
- **Task 6.1.1** — Create production Docker Compose configuration
  - Subtask: API service with health checks
  - Subtask: Web service
  - Subtask: PostgreSQL with volume + backup
  - Subtask: Redis
  - Subtask: Qdrant
  - Subtask: Nginx reverse proxy
- **Task 6.1.2** — Create environment-specific config templates
- **Task 6.1.3** — Document deployment runbook

#### Feature 6.2: Monitoring
- **Task 6.2.1** — Configure OpenTelemetry for production (OTLP endpoint)
- **Task 6.2.2** — Add custom business metrics (workflow count, approval time, etc.)
- **Task 6.2.3** — Set up health check endpoints for Docker health checks
- **Task 6.2.4** — Add structured logging with correlation IDs

#### Feature 6.3: Backup & Recovery
- **Task 6.3.1** — Configure PostgreSQL automated backups
- **Task 6.3.2** — Document restore procedure
- **Task 6.3.3** — Implement idempotency store persistence (currently in-memory)

### Epic 7: Database

#### Feature 7.1: Schema Verification & Migration
- **Task 7.1.1** — Review all entities match DB schema
- **Task 7.1.2** — Add migration for missing fields:
  - AssistantDefinition: PublishStatus, PublishedVersion, PublishedAt
  - WorkflowInstance: ElsaInstanceId (may already exist; verify)
  - User: LastLoginAt (exists), PasswordResetToken, PasswordResetExpires
- **Task 7.1.3** — Add migration for missing indexes
- **Task 7.1.4** — Verify cascading delete behavior

---

## 4. Sprint Plan

### Sprint 1 — Foundation & Core UI (Weeks 1-2)

**Goals:**
- Complete all Blazor CRUD dialogs
- Complete authentication flow
- Elsa Server integration setup
- P0 UI pages functional

**Deliverables:**
- All CRUD dialogs functional (Assistants, Workflows, Integrations, Settings)
- Login page with error/loading states
- Token refresh working
- Elsa Server configured with PostgreSQL persistence
- Elsa Studio embedded in Workflow Studio page

**Acceptance Criteria:**
- User can create, edit, delete assistants via UI
- User can create, edit, delete workflows via UI
- User can create, edit, delete integrations via UI
- User can manage users, roles, models via UI
- Login works with JWT (password + Entra ID)
- Token refresh works without session interruption
- Workflow Studio shows Elsa Studio in iframe
- All data grids have search, pagination, sorting

### Sprint 2 — Workflow Execution & Approvals (Weeks 3-4)

**Goals:**
- Complete workflow execution engine
- Human approval workflow end-to-end
- Approval UI in Operations Center
- Notification system

**Deliverables:**
- Workflow execution with step-by-step progress
- Approval request creation on approval steps
- Approval request list with approve/reject actions
- SignalR notifications for new approvals
- Escalation background service verified
- Elsa workflow ↔ R2WAI instance sync

**Acceptance Criteria:**
- User can execute a workflow from UI
- Approval step creates approval request
- Approver can approve/reject from Operations Center
- Approved/rejected status resumes Elsa workflow
- Escalation fires after configured timeout
- Workflow instance shows current step and status
- User receives notification when approval is requested

### Sprint 3 — Knowledge, AI & Operations (Weeks 5-6)

**Goals:**
- Complete knowledge indexing pipeline
- AI assistant publishing workflow
- Operations dashboard fully functional
- Audit viewer complete

**Deliverables:**
- Document indexing with progress
- Knowledge base search working end-to-end
- Assistant publish/unpublish workflow
- Operations dashboard with auto-refresh
- Audit log with detail view, filtering, export
- Metrics with charts

**Acceptance Criteria:**
- Uploading a document indexes it into Qdrant
- Searching a knowledge base returns relevant results
- Assistant can be published (draft → active)
- Operations dashboard shows real-time metrics
- Audit logs show all entity changes with old/new values
- Audit logs can be filtered and exported
- Workflow instance detail shows step timeline

### Sprint 4 — Security, Production Readiness & Polish (Weeks 7-8)

**Goals:**
- Complete security hardening
- Production deployment configuration
- Monitoring and backup setup
- Bug fixes and polish

**Deliverables:**
- Permission-based authorization
- Complete audit trail
- Production Docker Compose
- OpenTelemetry configured for production
- PostgreSQL backup configuration
- Idempotency store persistence
- Load testing results

**Acceptance Criteria:**
- All API endpoints enforce authorization
- Audit logs cover all sensitive operations
- Docker Compose starts all services
- Health checks pass for all services
- Logs include correlation IDs
- Backup and restore procedure documented and tested
- No P0/P1 bugs open

---

## 5. Database Tasks

### Missing Tables

| Table | Issue | Priority |
|-------|-------|----------|
| `Notifications` | Missing — needed for user notification system | P1 |
| `RefreshTokens` | Missing — needed for JWT refresh token rotation | P1 |
| `IdempotencyKeys` | In-memory only; needs persistent table | P2 |
| `SystemSettings` | Settings stored as key-value in DB vs config | P2 |

### Missing Migrations

| Change | Entity | Field | Priority |
|--------|--------|-------|----------|
| Add PublishStatus | AssistantDefinition | `PublishStatus` (Draft/Published/Archived) | P1 |
| Add PublishedVersion | AssistantDefinition | `PublishedVersion` (int) | P2 |
| Add PublishedAt | AssistantDefinition | `PublishedAt` (DateTime?) | P2 |
| Add PasswordResetToken | User | `PasswordResetToken` (string?) | P2 |
| Add PasswordResetExpires | User | `PasswordResetExpires` (DateTime?) | P2 |
| Add IsRead | Notification | `IsRead` (bool) | P1 |
| Add ElsaDefinitionId | Workflow | `ElsaDefinitionId` (string?) | P0 |
| Add TenantId to index | Various | Multi-tenant composite indexes | P1 |

### Schema Changes

| Change | Reason | Priority |
|--------|--------|----------|
| Add unique constraint on Users(ExternalId, TenantId) | Prevent duplicate Entra ID users per tenant | P1 |
| Add cascade behavior review | Ensure FK deletes don't orphan data | P1 |
| Add CreatedAt/ModifiedAt defaults | Use DB default values for consistency | P2 |

---

## 6. Backend Tasks

### Missing APIs

| Endpoint | Controller | Priority |
|----------|-----------|----------|
| `POST /api/v1/assistants/{id}/publish` | AssistantsController | P1 |
| `POST /api/v1/assistants/{id}/unpublish` | AssistantsController | P1 |
| `POST /api/v1/workflows/cancel` | WorkflowsController | P1 |
| `POST /api/v1/workflows/{id}/retry` | WorkflowsController | P2 |
| `GET /api/v1/notifications` | New NotificationsController | P1 |
| `POST /api/v1/notifications/{id}/read` | NotificationsController | P1 |
| `GET /api/v1/operations/approvals` | OperationsController (or ApprovalsController) | P0 |

### Missing Services

| Service | Reason | Priority |
|---------|--------|----------|
| `INotificationService` | Create/push notifications across SignalR + DB | P1 |
| `IAuditService` | Centralized audit logging with context enrichment | P1 |
| `IPublishingService` | Assistant publish/unpublish state machine | P1 |
| `ITokenRefreshService` | Refresh token management with rotation | P1 |
| `IBackgroundJobService` | Manage recurring jobs (escalation, cleanup) | P2 |

### Missing Business Logic

| Logic | Location | Priority |
|-------|----------|----------|
| Workflow step validation | WorkflowService | P1 |
| Approval policy matching by workflow type | ApprovalService | P1 |
| Document type detection by file signature | DocumentsController | P2 |
| Tenant provisioning on first login | AuthController | P1 |
| Password complexity validation | AuthController | P1 |
| Rate limiting configuration | RateLimitingMiddleware | P2 |

---

## 7. Frontend Tasks

### Mock / Skeleton Pages

| Page | Status | Missing | Priority |
|------|--------|---------|----------|
| Assistants.razor | Skeleton | Create/Edit/Chat dialogs, search | P0 |
| Workflows.razor | Skeleton | Create/Edit dialogs, Elsa Studio embed | P0 |
| Integrations.razor | Skeleton | Create/Edit dialogs | P0 |
| Operations.razor | Partial | Approval tab, detail views, filters, charts | P0 |
| Settings.razor | Partial | Create/Edit dialogs for users, roles, models | P0 |
| Login.razor | Skeleton | Error state, Entra ID button, loading | P0 |
| Home.razor | Skeleton | Dashboard widgets, recent activity | P2 |

### Missing API Integration

| Page | API Endpoint | Priority |
|------|-------------|----------|
| Assistants | `POST /api/v1/assistants` (create dialog) | P0 |
| Assistants | `PUT /api/v1/assistants/{id}` (edit dialog) | P0 |
| Assistants | `POST /api/v1/assistants/{id}/chat` (chat dialog) | P0 |
| Workflows | `POST /api/v1/workflows` (create dialog) | P0 |
| Workflows | `PUT /api/v1/workflows/{id}` (edit dialog) | P0 |
| Integrations | `POST /api/v1/integrations` (create dialog) | P0 |
| Integrations | `PUT /api/v1/integrations/{id}` (edit dialog) | P0 |
| Settings/Users | `POST /api/v1/admin/users` (create dialog) | P0 |
| Settings/Users | `PUT /api/v1/admin/users/{id}` (edit dialog) | P1 |
| Settings/Roles | `POST /api/v1/admin/roles` (create dialog) | P0 |
| Settings/Roles | `PUT /api/v1/admin/roles/{id}` (edit dialog) | P1 |
| Settings/Models | `POST /api/v1/admin/models` (create dialog) | P0 |
| Settings/Models | `PUT /api/v1/admin/models/{id}` (edit dialog) | P1 |
| Operations | `GET /api/v1/approvals/pending` (approval tab) | P0 |
| Operations | `POST /api/v1/approvals/{id}/approve|reject` | P0 |

### Missing Components

| Component | Used By | Priority |
|-----------|---------|----------|
| `AssistantCreateDialog.razor` | Assistants page | P0 |
| `AssistantEditDialog.razor` | Assistants page | P0 |
| `AssistantChatDialog.razor` | Assistants page | P0 |
| `WorkflowCreateDialog.razor` | Workflows page | P0 |
| `WorkflowEditDialog.razor` | Workflows page | P0 |
| `IntegrationCreateDialog.razor` | Integrations page | P0 |
| `IntegrationEditDialog.razor` | Integrations page | P0 |
| `UserCreateDialog.razor` | Settings page | P0 |
| `UserEditDialog.razor` | Settings page | P1 |
| `RoleCreateDialog.razor` | Settings page | P0 |
| `RoleEditDialog.razor` | Settings page | P1 |
| `ModelCreateDialog.razor` | Settings page | P0 |
| `ModelEditDialog.razor` | Settings page | P1 |
| `ApprovalCard.razor` | Operations page | P0 |
| `ApprovalActionDialog.razor` | Operations page | P0 |
| `AuditLogDetailDialog.razor` | Operations page | P1 |
| `NotificationBadge.razor` | MainLayout | P1 |
| `MetricsChart.razor` | Operations page | P1 |
| `ConfirmDialog.razor` | Shared (delete confirmations) | P1 |

### Missing Forms

| Form | Fields | Priority |
|------|--------|----------|
| Assistant Create | Name, Description, Type (dropdown), Model (dropdown), Knowledge Base (dropdown), System Prompt (textarea), Tools (multi-select) | P0 |
| Workflow Create | Name, Description, Type, Trigger | P0 |
| Integration Create | Name, Type (dropdown), Endpoint URL, Description, Configuration (JSON) | P0 |
| User Create | Email, First Name, Last Name, Password, Roles (multi-select) | P0 |
| Role Create | Name, Description, Permissions (checkbox list) | P0 |
| Model Create | Name, Provider (dropdown), Model ID, API Key, Endpoint, Max Tokens, Temperature, Top P | P0 |
| Approve/Reject | Comments (textarea) | P0 |

---

## 8. Workflow Tasks

### Elsa Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Elsa Studio embedding | Need to configure Studio route within Blazor app | P0 |
| Elsa persistence | PostgreSQL configured but verify migrations run | P0 |
| Elsa identity bridge | Elsa uses its own identity; need to bridge with R2WAI JWT | P1 |
| Custom activity registration | ApprovalStepActivity, InvokeSemanticKernelActivity registered | P0 |
| Elsa workflow definition sync | R2WAI workflows → Elsa definitions mapping | P1 |
| Elsa API proxy | Need to proxy Elsa Studio API through R2WAI auth | P1 |

### Approval Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Approval UI page | No dedicated approval list/action page in Operations | P0 |
| Approval notification | No push notification when approval is requested | P1 |
| Approval by role | API supports role-based approval; UI needs role selector | P1 |
| Escalation UI | No visual indicator of escalation level/time remaining | P2 |
| Approval policy management UI | Policies API exists; no UI to manage them | P1 |
| Multi-approver workflow | Policy supports min approvers; execution not implemented | P2 |

### Execution Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Step execution order | Sequence activity created; verify execution order | P0 |
| Workflow cancellation | No cancel endpoint or UI | P1 |
| Workflow retry | Failed steps cannot be retried | P2 |
| Workflow timeout | No timeout enforcement for long-running workflows | P2 |
| Execution telemetry | No step-level timing/duration tracking | P1 |
| Concurrent execution | Multiple instances of same workflow not prevented | P2 |
| Data passthrough | Workflow input data mapped to steps; verify completeness | P1 |

---

## 9. AI Tasks

### Semantic Kernel Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Kernel instance per model | Currently one kernel for all; need per-model kernels | P1 |
| Plugin registration | Plugins loaded at startup; need dynamic registration | P2 |
| Token usage tracking | No tracking of tokens consumed per request | P1 |
| Model fallback | No fallback if primary model fails | P2 |
| Prompt caching | No caching for identical prompts | P2 |
| Streaming via API | Streaming endpoint exists; UI integration needed | P0 |
| Function calling | Semantic Kernel function calling not configured | P2 |

### Assistant Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Publish/unpublish | No state machine for assistant lifecycle | P1 |
| Versioning | No version tracking for assistant definitions | P2 |
| Analytics | No assistant usage stats (queries, tokens, satisfaction) | P2 |
| System prompt templates | Hardcoded; need template management | P2 |
| Tool binding | Tools JSON field exists; no validation/execution | P2 |

### Knowledge Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Document chunking | Basic chunking implemented; need intelligent boundaries | P1 |
| Indexing progress | No progress reporting for large document indexing | P1 |
| Incremental indexing | Re-indexes everything; need incremental update | P1 |
| Multi-modal support | Text only; need PDF, image support | P2 |
| Source citation | Citations returned; need formatted display in UI | P1 |
| Embedding caching | Repeated embeddings not cached | P2 |

---

## 10. Security Tasks

### Authentication Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Token refresh | RefreshToken endpoint exists; rotation not implemented | P1 |
| Session management | No ability to revoke sessions | P2 |
| Entra ID validation | Token validation exists; need cert verification | P1 |
| Password policy | No password complexity enforcement | P1 |
| Account lockout | No lockout after failed attempts | P2 |
| MFA | Not in scope for MVP | P3 |
| HTTPS enforcement | Redirect configured; verify HSTS in production | P1 |

### Authorization Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Permission-based auth | Currently role-based only; need granular permissions | P1 |
| Resource-level auth | Tenant isolation works; no per-resource ownership checks | P1 |
| API key auth | No API key support for programmatic access | P2 |
| CORS hardening | Configured; need production-specific origins | P1 |
| CSRF protection | Antiforgery enabled; verify with Blazor Server | P1 |

### Audit Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Auto-audit logging | No EF SaveChangesInterceptor for automatic audit | P1 |
| Audit enrichment | IP, user agent, correlation ID not consistently captured | P1 |
| Audit viewer | Basic list works; need detail view, filtering, export | P1 |
| Audit retention | No cleanup policy for old audit logs | P2 |
| Sensitive data masking | Audit logs may contain PII; no masking | P2 |

---

## 11. Production Readiness Tasks

### Deployment Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Production Docker Compose | Development compose exists; need production version | P0 |
| Environment config | Config values hardcoded or env vars not documented | P0 |
| Health check integration | Docker HEALTHCHECK not configured | P1 |
| Reverse proxy | No Nginx/Caddy config for production | P1 |
| SSL certificate | No SSL termination configuration | P1 |
| CI/CD pipeline | GitHub Actions exist; verify deployment steps | P1 |
| Database migration automation | Migrations run on startup; need controlled process | P1 |

### Monitoring Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| OpenTelemetry exporter | Console exporter default; need OTLP for production | P1 |
| Business metrics | No custom metrics for business KPIs | P2 |
| Alerting | No alert rules configured | P2 |
| Dashboard | No Grafana dashboard | P2 |
| Log aggregation | Serilog writes to file; need central aggregation | P2 |
| Uptime monitoring | No external uptime check | P2 |

### Logging Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| Correlation IDs | Request-scoped correlation ID not implemented | P1 |
| Structured logging | Serilog configured; verify all logs use structured format | P1 |
| Log levels | Verify appropriate log level usage throughout | P1 |
| Sensitive data redaction | Passwords/keys may appear in logs | P1 |
| Log shipping | File logs not shipped to central location | P2 |

### Backup Gaps

| Gap | Detail | Priority |
|-----|--------|----------|
| PostgreSQL backup | No automated backup script | P1 |
| Qdrant backup | Vector data not backed up | P2 |
| Document storage backup | Local files not backed up | P1 |
| Restore procedure | No documented restore process | P1 |
| Backup schedule | No defined backup schedule | P1 |
| Disaster recovery | No DR plan | P2 |

---

## 12. Final Priority Order

### P0 — Must Complete for MVP

| # | Task | Epic |
|---|------|------|
| 1 | Implement all CRUD dialogs (Assistants, Workflows, Integrations, Settings) | Frontend |
| 2 | Implement Login page with error/loading/Entra ID states | Frontend |
| 3 | Implement token refresh in AuthDelegatingHandler | Backend |
| 4 | Embed Elsa Studio in Workflow Studio page | Frontend |
| 5 | Configure Elsa Server with PostgreSQL persistence | Workflow |
| 6 | Implement Approval UI (list, approve, reject) | Frontend |
| 7 | Complete WorkflowBridge step mapping and execution | Workflow |
| 8 | Wire ApprovalStepActivity to create approval requests | Workflow |
| 9 | Connect approval decision back to Elsa workflow resume | Workflow |
| 10 | Create Features/Integrations module with MediatR | Backend |
| 11 | Implement document indexing with progress | AI |
| 12 | Implement knowledge base search end-to-end | AI |
| 13 | Implement audit log auto-logging via EF interceptor | Backend |
| 14 | Create production Docker Compose configuration | Production |
| 15 | Environment configuration templates | Production |
| 16 | Implement SignalR notification integration for approvals | Backend |

### P1 — Important

| # | Task | Epic |
|---|------|------|
| 17 | Assistant publish/unpublish workflow | Backend |
| 18 | Permission-based authorization | Security |
| 19 | Resource-level ownership checks | Security |
| 20 | Entra ID certificate validation | Security |
| 21 | Approvals by role in UI | Frontend |
| 22 | Approval policy management UI | Frontend |
| 23 | Audit log detail view with diff/filter/export | Frontend |
| 24 | Metrics charts in Operations dashboard | Frontend |
| 25 | Notification creation on approval requests | Backend |
| 26 | Token refresh rotation | Security |
| 27 | NotificationBadge component in MainLayout | Frontend |
| 28 | Document intelligent chunking | AI |
| 29 | Incremental indexing | AI |
| 30 | OpenTelemetry OTLP exporter config | Production |
| 31 | Correlation IDs in logs | Production |
| 32 | PostgreSQL automated backup script | Production |
| 33 | Password policy enforcement | Security |
| 34 | Cascade delete behavior review | Database |
| 35 | Multi-tenant index review | Database |

### P2 — Later

| # | Task | Epic |
|---|------|------|
| 36 | Home page dashboard widgets | Frontend |
| 37 | Tool configuration UI in Assistants | Frontend |
| 38 | Workflow retry and cancellation | Workflow |
| 39 | Execution telemetry and timing | Workflow |
| 40 | System settings management UI | Frontend |
| 41 | Idempotency store persistence | Production |
| 42 | Audit log retention/cleanup job | Backend |
| 43 | Sensitive data masking in audit | Security |
| 44 | Grafana dashboard setup | Production |
| 45 | Account lockout policy | Security |
| 46 | API key authentication | Security |
| 47 | Embedding caching | AI |
| 48 | Model fallback configuration | AI |
| 49 | Qdrant backup procedure | Production |
| 50 | Load testing | Production |

---

## 13. Delivery Estimate

### Effort Breakdown by Epic

| Epic | Developer Days |
|------|---------------|
| Epic 1: Frontend Completion | 28 |
| Epic 2: Backend Completion | 18 |
| Epic 3: Elsa Runtime Integration | 15 |
| Epic 4: Knowledge & AI | 12 |
| Epic 5: Security | 10 |
| Epic 6: Production Readiness | 12 |
| Epic 7: Database | 5 |
| **Total** | **100** |

### Delivery Scenarios

| Scenario | Developer Days | Calendar Days | Notes |
|----------|---------------|---------------|-------|
| 1 Developer (full stack) | 100 | 100 (20 weeks) | Sequential; high risk of bottlenecks |
| 3 Developers (2 backend + 1 frontend) | 100 | 40 (8 weeks) | Parallel frontend/backend; realistic |
| 5 Developers (2 backend + 2 frontend + 1 DevOps) | 100 | 25 (5 weeks) | Aggressive; coordination overhead |

### Recommended: 3 Developers, 8 Weeks

| Role | Sprint 1-2 | Sprint 3-4 |
|------|-----------|-----------|
| Backend 1 | Elsa + Workflows + Approvals | Security + Production |
| Backend 2 | Application layer + Integrations | Knowledge Indexing + AI |
| Frontend 1 | All CRUD dialogs + Auth | Operations + Approvals + Polish |

### Risk Factors

| Risk | Impact | Mitigation |
|------|--------|------------|
| Elsa Studio embedding complexity | High | Spike in Week 1; fallback to Elsa standalone |
| Qdrant/vector search reliability | Medium | Implement text-search fallback |
| Entra ID configuration delays | Medium | JWT auth works independently |
| Blazor Server SignalR scaling | Low | Sufficient for MVP scale |
| Third-party API rate limits | Low | Implement retry + queue |
