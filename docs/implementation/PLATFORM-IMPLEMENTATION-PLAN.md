# R2WAI Government AI Platform — Implementation Plan

> **Author:** Product / Architecture Lead
> **Status:** Approved direction — freezes the target architecture. Feature development must not start until Phase 0 (Architecture Freeze) is complete.
> **Date:** 2026-08-10
> **Stack (as of 2026-08-10, now stale — see note below):** .NET 10, Blazor Server + MudBlazor, Clean Architecture, CQRS/MediatR, Semantic Kernel, Elsa (behind R2WAI abstraction), PostgreSQL 16 + pgvector, JWT + Entra ID + MFA, Docker Compose, Ollama / OpenAI / Azure OpenAI
> **Related docs:** [ROADMAP.md](../../ROADMAP.md) (current product roadmap and target architecture), [ADR-0001](../adr/0001-application-centric-architecture-freeze.md) (entity architecture freeze + 2026-09-29 amendment), [MVP-IMPLEMENTATION-PLAN.md](MVP-IMPLEMENTATION-PLAN.md) (legacy plan), [../architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) (recreated 2026-09-30, new path, not a restoration) — `ENTERPRISE_AUDIT_REPORT.md` still doesn't exist in this tree.
>
> **Note (2026-09-29):** This plan's product thesis and onboarding flow (§1's
> `CONNECT → DISCOVER → AUTO CONFIGURE → REVIEW → TEST → PUBLISH → MONITOR`) still hold — R2WAI
> 2.0's own onboarding journey (ROADMAP.md §2) is the same sequence. What's actually stale: the
> **Stack line above never matched the real build** (the client was built in React + TypeScript +
> MUI, not Blazor/MudBlazor — this predates R2WAI 2.0 and isn't a 2.0-driven change), and the
> **nav/studio model in §2 below is superseded** the same way ADR-0001 item 9 is (four studios →
> the current Home/Applications/Agents/Connections/Deployments/Activity/Settings nav; no
> user-facing Workflow Studio — see the ADR's amendment for what that actually changes versus just
> renames). The 996-line body below was not rewritten wholesale for 2.0 — treat it as a historical
> planning snapshot for anything not called out here, the same way `docs/audit/` is treated
> elsewhere in this project.

---

## 1. Product Definition

> **A secure, self-hosted, application-centric Government AI Platform that connects existing government applications and adds AI assistants, real-time API access, RAG knowledge, navigation, workflows, approvals, monitoring and governance — without requiring the existing application to be rebuilt.**

Core principle:

```text
CONNECT → DISCOVER → AUTO CONFIGURE → REVIEW → TEST → PUBLISH → MONITOR
```

Positioning statement that must survive every design decision:

> Existing government applications remain the **systems of record**. R2WAI is the secure AI interaction, orchestration, governance and automation layer on top of them.

This keeps the platform technically defensible, easier to integrate, safer for government data, and easier to sell/deploy than trying to replace existing applications.

---

## 2. Target Architecture

```text
                         R2WAI PLATFORM
                               │
              ┌────────────────┴────────────────┐
              │                                 │
       GLOBAL GOVERNANCE                  MODEL GOVERNANCE
              │                                 │
              └────────────────┬────────────────┘
                               │
                        DEPARTMENT
                               │
                        APPLICATION
                               │
       ┌───────────┬───────────┼───────────┬───────────┐
       │           │           │           │           │
      APIs       RAG      NAVIGATION     TOOLS      WORKFLOWS
       │           │           │           │           │
       └───────────┴───────────┼───────────┴───────────┘
                               │
                          AI ASSISTANT
                               │
                         POLICY ENGINE
                               │
                       TOOL/API GATEWAY
                               │
                  ┌────────────┴────────────┐
                  │                         │
              LIVE API                  KNOWLEDGE
                  │                         │
                  └────────────┬────────────┘
                               │
                         USER CONTEXT
                               │
                 ┌─────────────┼─────────────┐
                 │             │             │
               PUBLIC         USER         OFFICER
                 │             │             │
                 └─────────────┼─────────────┘
                               │
                        FLOATING AI
                          ASSISTANT
```

---

## 3. Product Modules

Exactly five modules. Everything in the product lives in one of these.

### A. Platform Governance (Super Admin)

```text
Platform
├── Departments
├── Applications
├── Model Governance
├── Global Security
├── Global Policies
├── Users & Roles
├── Audit
└── System Monitoring
```

### B. Application Studio (the main product)

```text
Application Studio
├── Applications
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

### C. Assistant Studio

```text
Assistant
├── Instructions
├── Model
├── Knowledge
├── Tools
├── API Access
├── Policies
├── Conversation
├── Channels
└── Evaluation
```

### D. Workflow Studio

```text
Workflow
├── Trigger
├── Steps
├── Conditions
├── API
├── AI
├── Approval
├── Notification
├── Schedule
├── Escalation
└── Execution
```

### E. Operations Center

```text
Operations
├── Application Health
├── API Health
├── AI Usage
├── RAG
├── Tool Calls
├── Workflows
├── Approvals
├── Errors
├── Security Events
└── Audit
```

---

## 4. Domain Model & Entity Migration Plan

### 4.1 Target domain model

```text
Tenant
│
├── Department
│   │
│   ├── Application
│   │   ├── ApplicationApi
│   │   ├── ApiEndpoint
│   │   ├── ApplicationEnvironment
│   │   ├── Assistant
│   │   ├── KnowledgeBase
│   │   ├── NavigationDefinition
│   │   ├── ToolDefinition
│   │   ├── Workflow
│   │   ├── Policy
│   │   ├── SecurityPolicy
│   │   ├── ApplicationConfiguration
│   │   └── ApplicationVersion
│   │
│   └── Users
│
├── ModelRegistry
├── GlobalPolicies
├── AuditLogs
└── PlatformConfiguration
```

### 4.2 Existing → target mapping

Existing entities (`src/R2WAI.Domain/Entities/`) move, are renamed, or are re-homed. **No production configuration is deleted** — use Disable → Archive → Soft Delete → Rollback (see §6).

| Existing entity | Action | Target |
|---|---|---|
| `Tenant` | KEEP | `Tenant` |
| — | **ADD** | `Department` (new ownership level between Tenant and everything else) |
| — | **ADD** | `Application` (the central entity) |
| `AssistantDefinition` | MODIFY | `Assistant` — re-homed under `Application` |
| `Chatbot`, `ChatbotChannel` | **MERGE** | `Assistant → Channel` (data migration + backfill) |
| `KnowledgeBase`, `KnowledgeBaseSource` | MODIFY | Re-homed under `Application`; add governance metadata (§13.2) |
| `Document` | KEEP | Re-homed under `Application`/`KnowledgeBase` |
| `ToolDefinition` | MODIFY | Re-homed under `Application`; add `ToolAuthorization` + risk metadata (§9) |
| `Workflow`, `WorkflowInstance`, `WorkflowStepExecution`, `WorkflowSchedule` | KEEP | Re-homed under `Application` |
| `ApprovalPolicy`, `ApprovalRequest` | KEEP | Unchanged, completed |
| `ModelConfiguration` | EVOLVE | `ModelRegistry` (approved-model registry, §18) |
| `Conversation`, `Message`, `MessageAttachment` | KEEP | Re-homed under `Assistant` (personal conversation history) |
| `AuditLog` | KEEP / EXPAND | Add AI + gateway + policy event schema (§21) |
| `User`, `Role`, `UserRole`, `ApiKey` | KEEP | Add department/application/jurisdiction claims (§11) |
| `WebhookEndpoint` | KEEP | Re-homed under `Application` |

---

## 5. New Entities

### 5.1 P0 — add first (blocking everything downstream)

| Entity | Purpose | Key fields |
|---|---|---|
| `Application` | Central entity — the external system | Name, TenantId, DepartmentId, Description, Category, BaseUrl, Environment, Status, LifecycleState, OwnerId |
| `Department` | Ownership level below Tenant | Name, Code, TenantId, HeadUserId, Policies |
| `ApplicationApi` | Registered API root for an application | ApplicationId, Name, BaseUrl, AuthScheme, CredentialRef, OpenApiSource |
| `ApiEndpoint` | Discovered endpoint | ApplicationId, ApiId, Method, Path, OperationId, Summary, InputSchema, OutputSchema, AuthRequired, DiscoveredBy |
| `ApplicationConfiguration` | Per-application settings | ApplicationId, Timeout, Retries, RAG threshold, ModelId, PromptTemplate, ... |
| `ApplicationVersion` | Versioned config snapshots | ApplicationId, Version, ConfigSnapshot, PublishedBy, PublishedAt, Status |
| `ApplicationEnvironment` | Dev/Staging/Prod | ApplicationId, Name, BaseUrl, CredentialRef, IsActive |
| `NavigationDefinition` | Registered safe navigation actions | ApplicationId, Label, ActionId, Path, Audience, Order |
| `ToolAuthorization` | Per-tool security metadata | ToolDefinitionId, RequiredPermission, RiskLevel, ConfirmationRequired, ApprovalRequired, AuditRequired, AllowedRoles, AllowedDepartments |
| `SecurityPolicy` | App-level security config | ApplicationId, AllowedOrigins, DataClassification, AuthRequirements |
| `AiPolicy` | App-level AI rules | ApplicationId, AllowedModels, PromptPolicy, ToolPolicy, DataPolicy |
| `RiskPolicy` | Risk rules per action type | ApplicationId, ActionType, RiskLevel, ApprovalWorkflowId, NotificationRoles |

### 5.2 P1 — second wave

| Entity | Purpose |
|---|---|
| `IntegrationProfile` | Reusable connector/auth profile (central connector registry, internal) |
| `ModelRegistry` / `ModelPolicy` | Approved models + per-model policy (governance) |
| `EvaluationSuite` / `EvaluationCase` | Pre-publish assistant evaluation (§23) |

---

## 6. Application Lifecycle

Every application follows a state machine. **Never immediately delete production configuration.**

```text
Draft → Discovering → Configuring → Testing → Published → Disabled → Archived
```

Supported transitions with full audit history:

```text
Disable → Archive → Soft Delete → Rollback
```

- `Soft Delete` = logical delete (already the codebase convention via `ApplySoftDeleteFilter`).
- `Rollback` = restore a prior `ApplicationVersion` / `Assistant` version.
- All transitions write to `AuditLog` (WHO/WHAT/WHEN/APPLICATION/ACTION).

---

## 7. Onboarding & Smart Wizard

### 7.1 Inputs the admin provides

```text
Application Name
Application URL
API/OpenAPI URL
Authentication
Environment
```

Then one button: **[ Connect & Auto Configure ]**.

### 7.2 What R2WAI performs automatically

```text
API Discovery → Schema Analysis → Authentication Analysis → Navigation Discovery
→ Knowledge Detection → Capability Detection → Tool Generation
→ Permission Suggestions → Assistant Generation
→ Security Configuration → Monitoring Configuration
```

### 7.3 Smart wizard — user only sees exceptions

Wizard steps: `1. Connect  2. Discover  3. Configure  4. Review  5. Test  6. Publish`

Inside **Configure**, API/RAG/Navigation/Model/Assistant/Tools/Security/Workflow/Monitoring are handled automatically. The user only sees problems:

```text
Application: Property Tax

✓ API connected
✓ 86 endpoints discovered
✓ Authentication configured
✓ 24 public endpoints identified
✓ 42 officer endpoints identified
✓ Knowledge configured
✓ Assistant generated
✓ Security policy generated

⚠ 3 endpoints need permission review
⚠ 1 financial endpoint requires approval

Configuration: 94% complete
```

### 7.4 UI default

Default screen is a checklist + **[Test Assistant]** / **[Publish]**. Everything advanced (Model, Prompt, Chunking, Embedding, Tool permissions, API timeout, RAG threshold, Security policy, Workflow settings) is behind **Advanced Configuration**.

---

## 8. Discovery Engine

### 8.1 Service

New service: `ApplicationDiscoveryService` (in `R2WAI.Infrastructure/Discovery/`).

Subcomponents (one class each, testable in isolation):

```text
OpenApiParser        — import + validate OpenAPI/Swagger documents
EndpointAnalyzer     — enumerate endpoints, methods, paths, schemas
SchemaAnalyzer       — request/response type analysis
AuthDetector         — detect auth scheme per endpoint (public/oauth/jwt/...)
CapabilityAnalyzer   — infer capability from operation summary/path
NavigationAnalyzer   — map navigation structure from the target app
PermissionAnalyzer   — suggest roles per endpoint
ToolGenerator        — produce draft ToolDefinition + ToolAuthorization per endpoint
AssistantConfigurator— draft assistant instructions, model, knowledge, tools
```

### 8.2 Example transformation

```text
GET /api/property/{id}
  → Capability: View Property
  → Tool: get_property
  → Access: User / Officer
  → Risk: Low
  → Audit: Required
```

### 8.3 Definition of done

OpenAPI import for a real government-style API produces a reviewable, persisted draft (endpoints + tools + permissions + assistant) that the admin can correct and approve — **without manual endpoint entry**.

---

## 9. Tool/API Gateway

**This is the critical security component.** The LLM never gets unrestricted HTTP access.

```text
AI → Tool Gateway → Authorization → Risk Policy → Confirmation → Application API
```

### 9.1 Every tool carries

```text
Tool
├── ApplicationId
├── Endpoint
├── Method
├── InputSchema
├── OutputSchema
├── RequiredPermission
├── RiskLevel
├── ConfirmationRequired
├── ApprovalRequired
└── AuditRequired
```

### 9.2 Gateway pipeline (enforced server-side, not by the model)

1. Resolve tool by name (registry — never a raw URL).
2. Check `ToolAuthorization` (role/department/application/jurisdiction).
3. Evaluate `RiskPolicy`.
4. If `ConfirmationRequired` → user confirmation step.
5. If `ApprovalRequired` → create `ApprovalRequest`, block until approved.
6. Execute via the application API (rate-limited, timeout-enforced).
7. Audit the invocation (who/tool/params/result).
8. Return sanitized result to the LLM.

### 9.3 Navigation enforcement

The LLM can only choose a **registered `NavigationDefinition` action**, never an arbitrary URL. The gateway treats navigation as tools.

---

## 10. Risk Classification

Four levels, applied automatically by the Discovery permission analyzer and overridable by the admin:

| Level | Meaning | Examples | Controls |
|---|---|---|---|
| **LOW** | Information retrieval | "Show my application status" | None beyond auth |
| **MEDIUM** | Non-sensitive updates | "Update my mobile number" | Confirmation required |
| **HIGH** | Sensitive record modification | "Approve application" | Officer authorization |
| **CRITICAL** | Financial / legal / irreversible | "Release payment" | Approval workflow |

---

## 11. RBAC + ABAC

Both are required. RBAC is the baseline; ABAC narrows it.

- **RBAC roles:** Super Admin, Department Admin, Officer, User. (Public is an access context, **not** an administrative role — §12.)
- **ABAC evaluates:** User · Role · Department · Application · Jurisdiction · Record ownership · Data classification · Action · Policy

Consequence: `Officer ≠ access to every department record`.

> **The AI model never determines permissions.** Authorization is computed in the policy/gateway layer only. User prompts and retrieved documents can never override authorization or system policies.

Existing codebase has `AdminOnly`, `TenantAccess`, `CanManageUsers`, `CanManageDocuments`, `CanManageWorkflows` policies — these are extended, and an ABAC evaluation service is added.

---

## 12. Access Contexts

| Context | Scope | Never exposed |
|---|---|---|
| **Public** (anonymous context, not a role) | Public FAQ · Public RAG · Public information · Public navigation · Public APIs | Private documents · private APIs · internal knowledge · officer functions · transactions |
| **User** | Own profile · own applications · own documents · own status · approved actions · personal conversation history | Other users' data · officer data |
| **Officer** | Department data · jurisdiction data · assigned cases · internal knowledge · authorized APIs · workflow · approval | Other departments/jurisdictions |
| **Department Admin** | Applications · Assistants · APIs · Knowledge · Tools · Navigation · Workflows · Policies · Department Users · Monitoring | Other departments · platform governance |
| **Super Admin** | Departments · Applications · Global Security · Model Governance · Platform Configuration · Global Policies · Audit · Monitoring | (platform-level) |

---

## 13. RAG Architecture & Governance

### 13.1 Pipeline

```text
Document → Upload → Validation → Extraction → Cleaning → Chunking → Embedding
→ PostgreSQL + pgvector → Retrieval → Optional Reranking → Context → LLM
```

**Critical requirement:** every production Knowledge Base must actually be indexed and searchable. The 2026-07-14 audit identified a live gap: text/URL sources persist but are **never enqueued for indexing** — only the file-upload path triggers indexing. Closing this gap is P0 (see Phase 1).

### 13.2 RAG governance metadata (every document)

```text
Department · Application · Classification · Owner · Version
Effective Date · Expiry Date · Status
```

Example:

```text
Property Tax Policy 2026
Application: Property Tax
Department: Revenue
Status: Published
Effective: 01-Apr-2026
Expires: 31-Mar-2027
```

The assistant preferentially uses current approved documents.

### 13.3 RAG vs Real-Time API — keep separate

```text
"What documents are required?"        → RAG
"What is my application status?"      → API
"Why was my application rejected?"    → API + RAG (combined)
```

The combined capability is one of R2WAI's strongest use cases and must be tested end-to-end in Phase 4.

---

## 14. Navigation Engine

`NavigationDefinition` per application. Example:

```text
Property Tax → My Properties → Payment → Receipt → Application Status
```

The LLM selects a **registered navigation action**, never an arbitrary URL (§9.3).

---

## 15. Floating Widget

Minimal, credential-free:

```html
<script src="r2wai-widget.js"
        data-application="PROPERTY-TAX">
</script>
```

- The widget pulls its configuration from R2WAI via the gateway (application binding).
- **It must never contain:** API secrets, model credentials, admin credentials, internal tokens.
- Anonymous visitors run with tools disabled (existing `enableTools: false` boundary in the chatbot path is preserved and moved under the gateway).

---

## 16. Authentication

```text
Existing Application → SSO / JWT / OAuth → R2WAI → User Context → Assistant
```

User context carried into every request:

```text
UserId · Department · Application · Role · Permissions · Jurisdiction
```

Existing JWT + Entra ID + TOTP MFA + `X-API-Key` remain. SignalR hubs keep `?access_token=`.

---

## 17. Model Governance (superset: AI Governance)

Central registry under Platform Governance:

```text
Model Registry
├── Ollama
├── Azure OpenAI
├── OpenAI
└── Other approved providers
```

Each model:

```text
Model
├── Provider
├── Capability
├── Context Size
├── Security Classification
├── Approved Departments
├── Allowed Applications
└── Status
```

- **Super Admin** approves models (Model Policy, Prompt Policy, Tool Policy, Data Policy, Risk Policy, Evaluation).
- **Department Admin** selects from the approved set.
- Local/self-hosted models (Ollama) remain first-class for air-gapped government deployment.
- `ModelConfiguration` evolves into this registry.

### AI Security Gateway

Independent of the assistant kernel, every request passes:

```text
Prompt Injection Detection → Sensitive Data Detection → PII Protection
→ Output Validation → Tool Authorization → Data Leakage Prevention → Policy Enforcement
```

---

## 18. Workflow & Approval

### 18.1 Keep Elsa behind R2WAI's abstraction

```text
R2WAI Workflow → Workflow Adapter → Elsa Runtime → Execution
```

### 18.2 Workflow node types

```text
Trigger · API · AI · Condition · Approval · Notification · Schedule · Escalation · End
```

### 18.3 Approval flow

```text
AI requests action
       ↓
Risk evaluation
       ↓
Approval required?
       │
   ┌───┴───┐
   No      Yes
   │        │
   ▼        ▼
 Execute   Approval → Officer/Admin → Execute
```

### 18.4 Worked example (citizen certificate)

```text
Citizen → AI Assistant → Eligibility Check → Document Check → Citizen Confirmation
→ Submit API → Officer Review → Approval → Certificate Generation → Notification
```

---

## 19. Audit

Every important event records:

```text
WHO · WHAT · WHEN · APPLICATION · ACTION · MODEL · TOOL · API · POLICY · RESULT
```

For AI interactions additionally:

```text
User Prompt · Assistant · Model · Prompt Version · Retrieved Sources · Tool · Tool Parameters
· Authorization · API Result · Final Response
```

- Sensitive information masked according to policy.
- `AuditLog` is already auto-populated on every mutation; **gateway invocations, policy decisions, and AI calls** are added in Phase 5/6.

---

## 20. Monitoring

| Surface | Metrics |
|---|---|
| Platform | CPU · Memory · Database · Storage · AI Provider · Workflow |
| Application | Requests · Latency · Errors · API failures · Active users |
| AI | Requests · Tokens · Models · RAG usage · Tool calls · Failures |
| Security | Unauthorized · Blocked tools · Policy violations · Suspicious activity |

Reuse the existing Operations Center, health checks, OpenTelemetry, and SignalR status hubs.

---

## 21. Evaluation

Before an assistant is published, a department admin runs an **Evaluation Suite**:

```text
FAQ · RAG · API · Navigation · Tool selection · Authorization
Prompt injection · Hallucination · Multilingual · Failure handling
```

```text
PASS → Publish
FAIL → Fix
```

`EvaluationSuite` / `EvaluationCase` entities (P1) persist cases and results; publishing requires a passing suite once available.

---

## 22. Configuration Versioning

Versionable units:

```text
Application · Assistant · Prompt · Tool · Workflow · Policy · Navigation · Knowledge
```

Supported operations:

```text
Publish · Rollback · Compare · Archive
```

`ApplicationVersion` (P0) snapshots config; rollback restores a prior snapshot with audit records. The existing `AssistantDefinition` draft/published/archived lifecycle is generalized to all versionable units.

---

## 23. Remove / Keep Registers

### Remove from the core Government product

```text
❌ Separate Chatbot entity          ❌ Chatbot Studio
❌ Agent Studio                     ❌ Separate Navigation Studio
❌ Separate Tool Studio (primary UX)❌ Separate Model Studio
❌ Separate Integration Studio      ❌ Image Studio
❌ Video Studio                     ❌ Audio Studio
❌ YouTube features                 ❌ Creative media gallery
❌ Duplicate Assistant/Agent concepts
```

These may return as future extensions but must not complicate the core platform.

### Keep

```text
✓ Clean Architecture  ✓ CQRS            ✓ Semantic Kernel  ✓ PostgreSQL  ✓ pgvector
✓ SignalR             ✓ Elsa            ✓ RAG              ✓ Assistant   ✓ Workflow
✓ Approval            ✓ Audit           ✓ Monitoring       ✓ JWT         ✓ Entra ID
✓ MFA                 ✓ Docker          ✓ Ollama           ✓ OpenAI/Azure OpenAI
✓ Embeddable widget
```

---

## 24. Infrastructure

### Pilot (nothing else mandatory)

```text
Reverse Proxy
     │
R2WAI Web/API
     │
PostgreSQL + pgvector
     │
Ollama
     │
Elsa
     │
Persistent Storage
```

### Do NOT make mandatory

```text
Qdrant        — removed (pgvector only)
Redis         — optional/later
MinIO         — optional/later
Kubernetes    — later
```

Introduce each only when scale or deployment requirements justify it. (Existing Docker Compose + local/MinIO storage switches stay.)

---

## 25. Target Project Structure

Aligns with existing projects; new feature folders added.

```text
src/
├── R2WAI.Domain
│   ├── Entities
│   │   ├── Tenant        Department      Application     Assistant
│   │   ├── Knowledge     Workflow        Tool            Policy
│   │   └── Audit
│   ├── ValueObjects
│   ├── Events
│   └── Enums
├── R2WAI.Application
│   ├── Applications    Assistants    Knowledge    Workflows
│   ├── Tools           Policies      Discovery    Evaluation
│   └── Operations
├── R2WAI.Infrastructure
│   ├── Persistence     AI            RAG          Integrations
│   ├── ToolGateway     Security      Storage      Monitoring
├── R2WAI.Api
│   ├── Applications    Assistants    Discovery    Tools
│   ├── Workflows       Chat          Operations
└── R2WAI.Web
    ├── Applications    Assistants    Workflows
    ├── Operations      Administration Shared
```

---

## 26. Implementation Roadmap

Phases are sequential. **Phase 0 freezes decisions; nothing else starts until it is signed off.**

### Phase 0 — Architecture Freeze (1 week)

Freeze (document in an ADR):

```text
✓ Final domain model    ✓ Final frontend decision    ✓ Final .NET version
✓ Final infrastructure  ✓ API conventions            ✓ Security model
✓ Role model            ✓ Application model
```

**Do not start feature development until this is frozen.**

### Phase 1 — Foundation Repair (2-3 weeks)

Fix the biggest existing technical debt first (audit-backed):

```text
✓ Database migrations      ✓ Seed data            ✓ Real API integration
✓ Frontend API client      ✓ Authentication       ✓ Tenant isolation
✓ RBAC                     ✓ Audit                ✓ Health checks
✓ Production configuration
```

**Includes:** close the RAG text/URL-source indexing gap (§13.1) and remove mock-data paths.

**DoD:** migrations run clean on a fresh DB; test suite green (baseline 407/407); no mock data in core flows; production config validation enforced.

### Phase 2 — Application Core (2-3 weeks)

```text
✓ Department       ✓ Application        ✓ Application API
✓ Environment      ✓ Application config ✓ Application versioning
✓ Application lifecycle
```

**DoD:** `Application`/`Department` entities live; tenant+department isolation enforced by query filters; lifecycle transitions audited; `ApplicationVersion` snapshot + rollback works.

### Phase 3 — Discovery Engine (3-4 weeks)

```text
✓ OpenAPI import            ✓ Endpoint discovery     ✓ Schema analysis
✓ Authentication detection  ✓ Capability detection   ✓ Navigation discovery
✓ Tool generation           ✓ Permission suggestions
```

**DoD:** §8.3 — OpenAPI import yields a reviewable draft (endpoints/tools/permissions/assistant) without manual entry.

### Phase 4 — AI Assistant (2-3 weeks)

```text
✓ Assistant    ✓ Model        ✓ System instructions    ✓ RAG
✓ Conversation ✓ Tool calling ✓ Policy                ✓ Context  ✓ Citations
```

**DoD:** assistant answers from current approved documents (RAG) and live data (API, combined) with citations; policy enforced; conversation history is user-scoped.

### Phase 5 — Secure Tool Gateway (2-3 weeks)

```text
✓ Tool registry    ✓ Authorization    ✓ RBAC    ✓ ABAC
✓ Risk levels      ✓ Confirmation     ✓ Approval
✓ API proxy        ✓ Audit            ✓ Rate limiting
```

**DoD:** no LLM path can bypass the gateway; each risk tier is enforced (confirmation/approval); every invocation is audited; ABAC denies cross-jurisdiction tool calls.

### Phase 6 — Workflow (2-3 weeks)

```text
✓ Elsa integration  ✓ Step chaining   ✓ Conditions
✓ API actions       ✓ AI actions      ✓ Approval
✓ Notifications     ✓ Scheduling      ✓ Escalation
✓ Execution monitoring
```

**DoD:** a multi-step workflow with approval runs end-to-end from the Studio and is audited.

### Phase 7 — Low-Code Wizard (3-4 weeks)

```text
Connect → Discover → Auto Configure → Review → Test → Publish
```

Add AI-generated recommendations (permission suggestions, FAQ generation). **DoD:** §7.3 experience — a non-technical department admin onboards an application using defaults only.

### Phase 8 — Floating Chatbot (1-2 weeks)

```text
✓ JavaScript widget   ✓ Application binding   ✓ Public mode
✓ Authenticated mode  ✓ Theme configuration  ✓ Mobile responsive
✓ Navigation          ✓ API integration
```

**DoD:** widget boots from `data-application`, holds no secrets, public visitors are tool-disabled.

### Phase 9 — Operations (2 weeks)

```text
✓ Dashboard    ✓ API monitoring    ✓ AI monitoring
✓ Workflow monitoring  ✓ Security monitoring  ✓ Audit  ✓ Alerts
```

**DoD:** Operations shows per-application health, AI usage, tool calls, and security events in real time.

### Phase 10 — Government Security (2-4 weeks)

```text
✓ MFA    ✓ SSO    ✓ RBAC    ✓ ABAC    ✓ Encryption
✓ Secret management    ✓ Rate limiting    ✓ CORS    ✓ Security headers
✓ Audit integrity      ✓ Data retention   ✓ Backup  ✓ Restore
✓ Vulnerability scanning   ✓ Penetration testing
```

### Phase 11 — Testing (cross-cutting)

```text
Unit · Integration · API · E2E · Security · RAG · AI evaluation
Workflow · Load · Failure recovery
```

### Phase 12 — Pilot (1 department, 1 application)

Start with **one department and one application** — never 20 applications at once.

```text
Department → Property/Revenue Application → Public Assistant
→ Authenticated User → Officer → Department Admin
```

---

## 27. Release / Acceptance Criteria

Minimum gate for any release:

```text
Build ✓   Migration ✓   Authentication ✓   Authorization ✓   Tenant isolation ✓
RAG ✓     API ✓         Workflow ✓         Audit ✓           E2E ✓   Security ✓
```

---

## 28. Pilot Success Criteria

The pilot must prove:

```text
Application connected < 30 minutes
API discovered automatically
Assistant generated automatically
Knowledge indexed successfully
Public FAQ works
Authenticated API works
Officer authorization works
Navigation works
Workflow works
Audit works
Monitoring works
```

> **The most important KPI: time from existing application to production AI assistant — hours, not weeks.**

---

## 29. Backlog

### P0 — Must Have (blocks everything)

```text
[x] Application entity        [x] Department entity
[x] Application isolation     [x] Database migrations
[ ] Real frontend/API integration   [ ] RAG indexing (close text/URL gap)
[ ] RBAC                      [ ] ABAC
[ ] Tool Gateway              [ ] API Gateway
[ ] Audit                     [ ] Workflow chaining
[ ] Approval                  [ ] Security hardening
```

### P1 — Product Core

```text
[ ] Application Discovery     [ ] OpenAPI import
[ ] Auto tool generation      [ ] Navigation discovery
[ ] Auto assistant generation [ ] Smart wizard
[ ] Model governance          [ ] AI policy
[ ] Risk policy               [ ] Evaluation
[ ] Floating widget           [ ] Operations Center
```

### P2 — Advanced

```text
[ ] Advanced analytics        [ ] Connector marketplace
[ ] Redis                     [ ] MinIO
[ ] Kubernetes                [ ] Multi-region
[ ] Advanced AI evaluation    [ ] Enterprise scaling
```

---

## 30. Final Strategy

```text
                  R2WAI
                    │
          GOVERNMENT AI PLATFORM
                    │
              APPLICATION
                    │
       ┌────────────┼────────────┐
       │            │            │
      DATA        AI           ACTION
       │            │            │
      API          RAG        WORKFLOW
       │         Assistant       │
       │            │         Approval
       └────────────┼────────────┘
                    │
              GOVERNANCE
                    │
        Security + RBAC + ABAC
                    │
                 AUDIT
                    │
               MONITORING
```

**The most important principle:**

> **Existing government applications remain the systems of record. R2WAI is the secure AI interaction, orchestration, governance and automation layer on top of them.**
