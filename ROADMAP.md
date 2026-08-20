# R2WAI Product Roadmap

> Government AI Application Platform

---

## Product Vision

R2WAI (Request To Work AI) is a self-hosted platform where government agencies connect their **existing applications** and R2WAI automatically discovers, configures, and delivers an **AI assistant** for each one — with knowledge (RAG), tools (API gateway), workflows, approvals, and governance — without writing code.

The core idea: **Application first.** R2WAI is not a collection of independent AI features; it is an application-centric platform. Connect a Property Tax app → get a Property Assistant. Connect the Revenue app → get a Revenue Assistant. The assistant, its knowledge, tools, permissions, and workflows all belong to that application.

The platform is built on .NET 10 with Semantic Kernel and Elsa workflow engine, supports multiple AI models (OpenAI, Azure OpenAI, Ollama), and provides full data sovereignty through on-premise deployment. It separates **RAG** (FAQ / policy / documents) from **live data** (status / transactions via APIs), and it **never lets the AI model call a government API directly** — every tool call passes through a secure, policy-enforced Tool/API Gateway with RBAC + ABAC.

---

## Strategic Pivot

### From

```text
Tenant
 ├── Assistant
 ├── Chatbot
 ├── KnowledgeBase
 ├── Workflow
 └── Model
```

### To

```text
Tenant
 └── Department
      └── Application
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

### Why

| Change | Reason |
|---|---|
| **Application is the central entity** | Government deployments are organized around business systems, not around AI features. Onboarding = "connect this app," not "build a chatbot." |
| **Auto Discovery + Low-Code Wizard** | Connects an existing app → understands it → generates configuration. Admin only reviews, tests, publishes. |
| **Tool/API Gateway (RBAC + ABAC + policies)** | The LLM must never directly call government APIs. Every tool needs role, permission, risk level, confirmation, approval, and audit metadata. |
| **Remove duplicated concepts** | `Chatbot` merges into `Assistant → Channels`. `Agent` / `Copilot` / `AI Employee` collapse into `Assistant`. |
| **Remove standalone studios** | Chatbot/Model/Integration/Navigation/Tool/Media studios fold into application areas or Administration. |
| **Separate RAG from live data** | Policy/documents → RAG; current status/transactions → API. Combined by the assistant, governed differently. |
| **AI Governance** | Super Admin approves models; Department Admin selects from the approved set. |

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full target architecture and component action register, and [docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md](docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md) for the code-ready implementation plan (phases, entities, gateway/risk specs, acceptance criteria).

---

## Target Personas

| Persona | Role | Pain Point | How R2WAI Helps |
|---|---|---|---|
| **Super Admin** | Governs the platform | No control over which AI models are used or where data goes; no enforcement of AI policy | Global/model/security governance: approved model registry, model/prompt/tool/data/risk policies, full audit |
| **Department Admin** | Manages applications and assistants for one department | Building AI for each system requires consultants and weeks of work | Connect an application → auto-discovery → auto-configuration → review → test → publish in hours |
| **Officer** | Does the work (cases, approvals, jurisdiction) | Scattered systems; no unified view of assigned cases or authorized actions | Assistant grounded in internal knowledge and authorized workflows, scoped to department/jurisdiction/assigned cases |
| **Citizen / Public** | Anonymous website visitor | Can't find answers on public websites; gets lost in navigation | Public FAQ / RAG / navigation via the floating widget, limited to public data only |
| **IT Admin** | Deploys and manages the platform | AI tools require sending government data to third-party clouds | Self-hosted deployment, multi-model support (Ollama for air-gapped), tenant/department isolation |

---

## Success Metrics

| Metric | Pilot Target | Scale Target |
|---|---|---|
| Time to first working assistant | < 1 hour (discovery + review) | < 30 minutes |
| Approval cycle time | Same-day | < 2 hours (with SLA tracking) |
| Knowledge query accuracy (top-5 recall) | > 70% | > 85% (hybrid search) |
| Tool gateway policy violations caught | 100% (blocked before execution) | 100% |
| Concurrent users supported | 50 | 500+ |
| Deployment time from scratch | < 30 minutes | < 15 minutes (K8s, later) |
| System uptime | 95% | 99.5% |

---

## Current State Snapshot

> Verified against the codebase 2026-06-23; security hardening pass completed 2026-07-14 (see [ENTERPRISE_AUDIT_REPORT.md](ENTERPRISE_AUDIT_REPORT.md)). **This snapshot predates the Department/Application entities (added 2026-08-10) and the 2026-08-20 UI/UX redesign pass** — see [ARCHITECTURE.md](ARCHITECTURE.md#adoption-status) for current adoption status. That pass delivered role-based navigation (5 personas via `RolePersona`, UI-only — see README's [UI/UX & Role-Based Navigation](README.md#uiux--role-based-navigation) section), the Automations rename + wizard + simple detail/edit view, an Assistant Studio simple-view/edit split, an Integrations connection-test action, and closed a real gap where no role could ever be assigned to a user after creation. It does **not** touch Phases 1–2, 6 (Discovery Engine, Tool/API Gateway, AI Governance) below, which remain `[target]`.

### What exists and works

- ASP.NET Core 10 + Clean Architecture (4 layers), Blazor Server + MudBlazor, PostgreSQL 16 + pgvector, Docker Compose
- JWT auth (15-min access + 7-day refresh rotation), Entra ID SSO, TOTP MFA, API-key scheme
- Tenant isolation via EF Core global query filters; audit log auto-populated on mutation
- AI assistant studio with 4 Semantic Kernel plugins (RAG, Document, Workflow, Assistant), streaming chat, citations
- RAG pipeline for file uploads (PDF/DOCX/XLSX/PPTX → extract → chunk → embed → pgvector search)
- Elsa workflow engine with approval steps, approval lifecycle with escalation background service
- Tool framework (`ITool`, `ToolRegistry`, `HttpTool`, `EmailTool`), integration marketplace UI
- Embeddable chatbot widget (anonymous visitors: tools disabled by design)
- Operations center: monitoring dashboard, audit viewer, health checks, OpenTelemetry, Serilog
- Test suite: **407/407 passing** across the solution; Playwright E2E scripts

### Known gaps to close before new features (P0)

1. Database migrations for the new entity model
2. Real frontend ↔ backend integration (remove mock-data paths)
3. Real KB indexing / embeddings (text/URL source indexing is broken)
4. Workflow step chaining
5. Real approval UI
6. Tenant isolation verification across new entities
7. RBAC enforcement review + ABAC introduction
8. API integration (Application → API → Tool pipeline)
9. Security hardening (concurrency tokens, gateway controls)
10. Production configuration validation

---

## Roadmap — Migration to the Application-centric Platform

### Phase 0 — Architecture Consolidation (Weeks 1-3)

**Goal:** Restructure the domain and UI around `Application`; remove duplication. No new user-facing features.

#### Deliverables

- Introduce `Department` and `Application` entities; re-home Assistant, Knowledge, Tools, Workflows, Policies, APIs under `Application`
- Merge `Chatbot` into `Assistant → Channels` (migration + data backfill)
- Collapse Assistant/Agent/Chatbot concepts into a single `Assistant` with capabilities: Knowledge, Tools, Workflow, Model, Policies, Channels
- Remove standalone Chatbot / Model / Integration / Navigation / Tool / Media studios from primary UX; keep a central connector registry internally
- Remove Qdrant from the stack (pgvector only)
- Close the P0 technical gaps (see above): migrations, mock-data removal, KB indexing, workflow chaining, approval UI, tenant isolation verification, RBAC hardening, config validation

**Exit criteria:** new domain model compiles with migrations; no separate chatbot entity; studio navigation reduced to 4 primary areas.

---

### Phase 1 — Application Studio + Discovery Engine (Weeks 4-6)

**Goal:** Make Application Studio the main product and build auto-discovery.

#### Deliverables

- Application Studio UI: Applications list + per-application Overview / Discovery / APIs / Knowledge / Navigation / Assistant / Tools / Workflows / Security / Policies / Testing / Monitoring / Publish
- Application Discovery Engine: OpenAPI/Swagger discovery, endpoint discovery, authentication detection, API schema analysis, navigation discovery, capability detection
- Auto-configuration generators: tool suggestion, FAQ generation, permission suggestion, assistant configuration draft
- Review workflow: admin reviews discovered configuration, fixes issues, re-runs discovery

**Exit criteria:** connecting an existing Swagger-enabled application produces a reviewable assistant configuration without manual API entry.

---

### Phase 2 — Tool/API Gateway + Policy Engine (Weeks 7-10)

**Goal:** The LLM never calls government APIs directly.

#### Deliverables

- Tool/API Gateway: every tool call passes through authorization → policy → gateway → application API
- Per-tool metadata: Application, Endpoint, HTTP Method, Role, Permission, Risk Level, Confirmation Required, Approval Required, Audit Required
- Policy engine with **configuration inheritance**: Global → Department → Application → Assistant → User Context
- **RBAC + ABAC**: authorization considers Role + Department + Application + Jurisdiction + Record ownership + Action + Policy
- AI Governance foundation: Model Registry, Approved Models, Model Policy, Prompt Policy, Tool Policy, Data Policy, Risk Policy
- Gateway audit logging for every invocation

**Exit criteria:** an approved user can execute an approved tool; any non-authorized or non-compliant tool call is blocked and audited; the AI model itself holds no permission authority.

---

### Phase 3 — Adaptive Wizard + Default-Configured UX (Weeks 11-12)

**Goal:** One-click onboarding with advanced config hidden by default.

#### Deliverables

- Adaptive wizard flow: Connect → Auto Discover → Auto Configure → Review Issues → Test → Publish
- Default screen shows a review checklist (`✓ Connected ✓ API discovered ✓ Knowledge configured ...`) with **[Test Assistant]** / **[Publish]**
- Advanced Configuration disclosure (Model, Prompt, Chunking, Embedding, Tool permissions, API timeout, RAG threshold, Security policy, Workflow settings)
- Knowledge split UI: RAG sources (FAQ/policy/documents) vs live API data — never treated as the same thing

**Exit criteria:** a non-technical department admin can onboard an application end-to-end using defaults only.

---

### Phase 4 — Security Model: Public / User / Officer (Weeks 13-14)

**Goal:** Correct, scoped access for each audience.

#### Deliverables

- Public (anonymous context, not a role): Public FAQ, Public RAG, Public Navigation, Public APIs via floating widget
- User: own information, own applications, own documents, approved transactions
- Officer: department, jurisdiction, assigned cases, internal knowledge, authorized workflows
- Role hierarchy: Super Admin → Department Admin → Officer / User (Public is an anonymous context)
- Floating widget re-architecture: Website → R2WAI Floating Widget → Application ID → R2WAI Gateway → Application Assistant

**Exit criteria:** each audience sees exactly its scope; anonymous visitors cannot reach any tool or internal knowledge.

> **Partially delivered 2026-08-20** (UI navigation only — the scoped *data* access this phase also calls for, e.g. an Officer's "assigned cases" or a User's "own applications" filtered API results, is not built): `RolePersona` gives Super Admin/Department Admin/Officer/Citizen/Public each their own nav menu, and role assignment (previously impossible) now works end-to-end. What's still open: literal `Officer`/`DepartmentAdmin` role names in the DB (today it's a mapping from existing roles like `Admin`/`WorkflowManager`), the ABAC/scoped-query layer, and the floating-widget re-architecture.

---

### Phase 5 — Workflow & Approvals Completion (Weeks 15-16)

**Goal:** Complete the workflow/approval surface (Elsa kept behind the R2WAI abstraction).

#### Deliverables

- Workflow step chaining and branching (step output variable mapping)
- Visual designer embedding / drag-and-drop step builder
- Real approval UI: approve/reject/escalate with SLA timers, comments, history
- Workflow templates library (department use cases: case intake, document request, payment approval)
- Application-scoped workflow visibility in the Application Studio

**Exit criteria:** a multi-step workflow with human approvals runs end-to-end from the Studio and is fully audited.

---

### Phase 6 — Operations & Governance Expansion (Weeks 17-18)

**Goal:** Monitoring, audit, and governance at scale.

#### Deliverables

- Gateway/invocation monitoring in Operations Center (per-application dashboards)
- Audit expansion: tool invocations, policy decisions, model usage, permission changes
- AI evaluation: assistant answer quality, RAG recall, tool misuse attempts
- Audit log export (CSV/JSON), advanced filtering
- Concurrency tokens on all entities

**Exit criteria:** every sensitive action is visible in Operations; governance reports answer "who did what, with which model, through which tool, when."

---

### Phase 7 — Production Hardening & Pilot (Weeks 19-21)

**Goal:** First government pilot deployment.

#### Deliverables

- Production configuration validation across real environments
- Load testing under pilot conditions
- Security review of gateway/policy paths
- Pilot runbook, training, and support runbook for department admins
- Docker Compose production configuration finalized (Redis/MinIO only if required)

**Exit criteria:** pilot department onboarded with at least one live application assistant; success metrics met.

---

### Post-Pilot (Later)

- Kubernetes deployment validation with HPA scaling
- Redis cache + SignalR backplane activation
- Scheduled workflows (cron triggers via Elsa.Scheduling), webhook inbound triggers
- URL source crawling for knowledge bases
- Advanced AI (multi-agent orchestration) — only after governance is proven
- Prometheus + Grafana monitoring stack

See [docs/COMPLETE_ROADMAP.md](docs/COMPLETE_ROADMAP.md) for the legacy sprint plans, epic backlogs, and task-level breakdowns (pre-pivot).

---

## Priority Matrix

### P0 (Application-Centric Foundation)

```
Application Entity + Migrations   Department Entity
Assistant + Channels (Chatbot merge)
Discovery Engine                  Auto-Config Generators
Tool/API Gateway                  RBAC + ABAC + Policies
Configuration Inheritance         AI Governance (Model Registry)
KB Indexing Complete              Workflow Chaining + Approval UI
Audit Expansion                   Frontend ↔ Backend Integration
```

### P1 (Important)

```
Adaptive Wizard                   Default-Hidden Advanced Config
Public/User/Officer Security      Floating Widget (Application ID)
Application Monitoring            Template Workflows
Audit Export                      Concurrency Tokens
```

### P2 (Later)

```
Kubernetes                        Redis/MinIO activation
Scheduled/Webhook Workflows       Multi-agent orchestration
Prometheus + Grafana              Additional channels
```

---

## Delivery Estimate

> Estimates for remaining work to reach each milestone under the pivoted plan.

| Team Size | Pilot-Ready (Phases 0-7) | Scaled Enterprise |
|---|---|---|
| 1 Developer | ~21 weeks | ~30 weeks |
| 3 Developers | ~8 weeks | ~12 weeks |
| 5 Developers | ~5 weeks | ~8 weeks |

---

## Pilot Success Criteria

A department admin can:

1. Connect an existing application (Swagger/URL)
2. Have R2WAI auto-discover APIs, navigation, and capabilities
3. Review and fix the auto-generated assistant configuration
4. Test the assistant (chat with streaming, RAG citations)
5. Execute a policy-governed tool call through the gateway (RBAC + ABAC enforced, audited)
6. Run a workflow with human approvals
7. Publish the assistant to the floating widget and an application channel
8. Confirm a public visitor sees only public content
9. Confirm an officer sees only jurisdiction-scoped data
10. View monitoring and audit trails for all assistant/tool activity

If all 10 work end-to-end, **the R2WAI government pilot is successful.**
