# R2WAI Product Roadmap

> Enterprise AI Work Execution Platform

---

## Product Vision

R2WAI (Request To Work AI) is a self-hosted enterprise platform where business teams create AI assistants that can reason over company documents, execute multi-step approval workflows, and take action through system integrations — without writing code. Built on .NET 10 with Semantic Kernel and Elsa workflow engine, it supports multiple AI models (OpenAI, Azure OpenAI, Ollama) and provides full data sovereignty through on-premise deployment.

The platform unifies three capabilities that enterprises typically buy separately: **AI chat with RAG-powered knowledge search**, **visual workflow automation with human-in-the-loop approvals**, and **a connector framework for external system integration** — all governed by multi-tenant isolation, role-based access, and a complete audit trail.

---

## Target Personas

| Persona | Role | Pain Point | How R2WAI Helps |
|---|---|---|---|
| **Enterprise IT Admin** | Deploys and manages the platform | AI tools require sending company data to third-party clouds; no control over models or data residency | Self-hosted deployment with multi-model support (Ollama for air-gapped), AES-256-GCM encryption, tenant isolation via EF Core query filters |
| **Business Operations Manager** | Designs approval workflows | Approval chains are email-based, take days, have no SLA tracking, and lack bottleneck visibility | Visual workflow designer (Elsa), automated escalation with configurable SLA timeouts, real-time workflow run monitoring |
| **Knowledge Worker** | Uses AI assistants daily | Spends 30%+ of time searching across SharePoint, shared drives, and wikis for answers | AI assistants that answer from uploaded company documents (PDF, DOCX, XLSX, PPTX) with source citations via pgvector RAG |
| **Compliance / Approver** | Reviews and approves requests | No centralized audit trail; can't answer "who approved what, when, and why" | Built-in approval governance with full audit logging on all mutations, approval history with comments, exportable audit logs |

---

## Success Metrics

| Metric | MVP Target | Enterprise Target |
|---|---|---|
| Time to first working assistant | < 15 minutes | < 10 minutes |
| Approval cycle time | Same-day | < 2 hours (with SLA tracking) |
| Knowledge query accuracy (top-5 recall) | > 70% | > 85% (hybrid search) |
| Concurrent users supported | 50 | 500+ |
| Deployment time from scratch | < 30 minutes | < 15 minutes (K8s) |
| System uptime | 95% | 99.5% |
| Document indexing latency | < 60 seconds per document | < 30 seconds per document |

---

## MVP Phases

> Status verified against codebase on 2026-06-23

### Phase 0 — Foundation (Weeks 1-2) | 95% Complete

**Goal:** Stabilize platform foundation.

#### Deliverables

- ASP.NET Core 10 + Clean Architecture (4 layers)
- Blazor Server + MudBlazor UI
- PostgreSQL 16 with pgvector
- Docker Compose environment
- JWT Authentication (15-min access + 7-day refresh rotation)
- Entra ID Integration (token exchange via EntraIdAuthService)
- CI/CD Pipeline (GitHub Actions)
- Database migration framework (7 migrations applied)
- AES-256-GCM credential encryption

#### Exit Criteria

- Login + refresh token flow works end-to-end
- Database migrations run cleanly
- Docker Compose starts all services

**Remaining:** CSRF token validation on critical Blazor endpoints

---

### Phase 1 — Core Platform (Weeks 3-4) | 90% Complete

**Goal:** Create R2WAI Portal and App Hub.

#### Deliverables

##### Portal

- Dashboard with real metrics and KPI cards (Home.razor — 641 lines)
- Studio-grouped sidebar navigation
- User Profile with avatar upload
- Role Management
- Notification inbox with real-time SignalR updates

##### Studios

- AI Assistant Studio shell
- Workflow Studio shell
- Integrations Studio shell
- Operations Center shell
- Settings shell

##### Security

- Role-based route protection

#### Exit Criteria

- Users can navigate all studios
- Role-based authorization works

**Remaining:** Dedicated search results page (currently embedded in home), push notification Blazor integration

---

### Phase 2 — AI Assistant Studio (Weeks 5-6) | 85% Complete

**Goal:** Create and manage AI assistants with domain-specific configurations.

#### Deliverables

##### Assistant Management

- Create / Edit / Delete / Publish assistants (Assistants.razor — 763 lines)
- Template engine with GenerateAssistant support
- 5 domain types: HR, IT, Finance, Legal, Procurement

##### Assistant Configuration

- System prompt with template pre-fill
- Tool binding (basic structure)
- Knowledge base linking
- Workflow assignment

##### Semantic Kernel Integration

- Kernel setup with 4 plugins (RAG, Document, Workflow, Assistant)
- Chat completion with streaming (Server-Sent Events)
- Tool invocation via auto function calling
- Citation rendering from knowledge base results

#### Exit Criteria

- Assistant creation works
- Assistant chat works with streaming responses
- Assistant publishing lifecycle works

**Remaining:** Full tabbed editor content for Knowledge/Tools/Publish tabs, tool binding UI refinement

---

### Phase 3 — Workflow Studio (Weeks 7-9) | 75% Complete

**Goal:** Visual workflow design and execution via Elsa.

#### Deliverables

##### Elsa Integration

- Elsa 3.7 registered with EF Core persistence
- WorkflowBridge for R2WAI ↔ Elsa communication
- Custom activities for approval steps

##### Workflow Management

- Create / Publish / Execute workflows
- Visual designer canvas (WorkflowDesigner.razor)
- Step execution tracking (WorkflowStepExecution entity)

##### Approval Framework

- Approval request lifecycle (create, approve, reject, escalate, cancel)
- Policy-based routing with role-based approvers
- Escalation background service (5-min interval)
- Card-based approval UI with approve/reject actions

#### Exit Criteria

- Workflow execution works with step tracking
- Human approvals work with escalation

**Remaining:** Complete Elsa Studio visual designer embedding, drag-and-drop step builder, step output variable mapping, complex branching UI

---

### Phase 4 — Integration Studio (Weeks 10-11) | 80% Complete

**Goal:** Enable external system connectivity.

#### Deliverables

##### Tool Framework

- Tool Registry (ToolRegistry.cs)
- Tool Execution Engine

##### Connectors

- HTTP Tool with full request/response handling
- Email Tool with templating
- 20+ pre-built connector templates (Salesforce, HubSpot, Slack, Teams, etc.)
- Integration marketplace UI (Integrations.razor — 150+ templates)

##### Testing & Auth

- Connection testing endpoint (Test in IntegrationsController)
- Integration toggle (enable/disable)

##### Assistant Integration

- Assistant → Tool invocation
- Workflow → Tool invocation

#### Exit Criteria

- Assistant can call external APIs
- Workflow can call external APIs
- Connection testing returns success/failure

**Remaining:** Advanced auth config UI (OAuth2 flows), retry/circuit breaker (Polly), response mapping (JSON path → workflow variable)

---

### Phase 5 — Knowledge & RAG (Weeks 12-13) | 85% Complete

**Goal:** AI assistants answer from company documents with citations.

#### Deliverables

##### Document Management

- Upload documents (PDF, DOCX, XLSX, PPTX)
- Document processing pipeline (upload → extract → chunk → embed → index)
- Configurable chunking (1000 char chunks, 200 overlap)

##### Knowledge Indexing

- OpenAI embedding generation (text-embedding-3-small, 1536 dims)
- pgvector cosine similarity search (PgVectorService.cs — 150+ lines)
- Vector + metadata filtering (JSONB payload in vector_embeddings table)
- Search ranking (1 - cosine_distance scoring)

##### Semantic Kernel Integration

- RAG plugin for knowledge-grounded responses
- Citation support with source document references

#### Exit Criteria

- Knowledge search works with ranked results
- Assistant answers from documents with citations

**Remaining:** BM25 full-text search (currently vector-only), LLM re-ranking, advanced metadata filtering UI

---

### Phase 6 — Operations Center (Weeks 14-15) | 85% Complete

**Goal:** Monitoring, audit, and governance.

#### Deliverables

##### Monitoring

- Dashboard with KPI cards (Operations.razor — 200+ lines)
- Workflow activity feed with real-time updates
- Auto-refresh toggle with live indicator
- Alert management with severity levels

##### Audit

- Audit log viewer with filtering (AuditLogsController)
- Correlation ID tracking (CorrelationIdMiddleware with X-Correlation-Id header)
- Metrics API endpoint

##### Health

- 5+ health check implementations (DB, AI Provider, Memory, Redis, overall)
- System health status display

#### Exit Criteria

- Operations dashboard shows live metrics
- Audit logs are queryable
- System health reflects actual service status

**Remaining:** Audit log export (CSV/JSON download), advanced date-range filtering

---

### Phase 7 — Production Hardening (Weeks 16-18) | 75% Complete

**Goal:** Production-ready MVP.

#### Deliverables

##### Security

- Authorization review
- Secret management via environment variables (.env.example)
- AES-256-GCM credential encryption (complete)

##### Deployment

- Docker Compose production config (docker-compose.production.yml)
- Automated database backup script (backup.sh — daily with retention)
- Database restore script (restore.sh)
- Health endpoint (/health)

##### Logging & Observability

- Serilog structured logging with correlation ID enrichment
- OpenTelemetry integration
- Security headers middleware (CSP, HSTS, X-Frame)

##### Testing

- API test suite (25+ test files)
- Browser-based E2E tests (5 .mjs test files)
- 82 unit/integration tests passing

#### Exit Criteria

- Production deployment from scratch in < 30 minutes
- Backup/restore verified
- Security review completed

**Remaining:** Load testing under real conditions, Kubernetes manifest validation, database migration rollback documentation

---

## Post-MVP Outlook

### Enterprise V1 (target: Sep 2026)

- Permission matrix UI (role × permission checkbox grid)
- Tenant configuration and provisioning via UI
- User invitation flow with email onboarding
- Workflow templates library (Invoice Approval, Purchase Request, etc.)
- Workflow versioning (draft → published → archived)
- Integration test suite with 60%+ coverage on critical paths
- Usage analytics charts (workflows/day, messages/day)

### Enterprise V2 (target: Oct 2026)

- Kubernetes deployment validation with HPA scaling
- Redis cache activation for multi-instance SignalR backplane
- Scheduled workflows (cron triggers via Elsa.Scheduling)
- Webhook receivers for inbound workflow triggers
- URL source crawling for knowledge bases
- Advanced AI (multi-agent orchestration)
- Prometheus + Grafana monitoring stack

See [docs/COMPLETE_ROADMAP.md](docs/COMPLETE_ROADMAP.md) for detailed sprint plans, epic backlogs, and task-level breakdowns.

---

## Competitive Context

R2WAI occupies the gap between general-purpose AI chat tools and heavyweight enterprise platforms. Unlike cloud-only solutions, it is self-hosted with full data sovereignty and multi-model support.

| Capability | R2WAI | Copilot Studio | ServiceNow Now Assist | Zapier AI |
|---|---|---|---|---|
| Self-hosted / on-prem | Yes | No | No | No |
| Multi-model (OpenAI + Ollama) | Yes | No (Azure only) | No | Limited |
| Visual workflow designer | Yes (Elsa 3.7) | Yes (Power Automate) | Yes | Limited |
| RAG / Knowledge search | Yes (pgvector) | Yes (M365 Graph) | Yes (proprietary) | No |
| Human-in-the-loop approvals | Built-in with escalation | Via Power Automate | Yes | No |
| Enterprise audit trail | Full (all mutations) | Partial | Yes | No |
| Open .NET stack | Yes (MIT-friendly) | Proprietary | Proprietary | Proprietary |
| Air-gapped deployment | Yes (Ollama) | No | No | No |

---

## Priority Matrix

### P0 (Must Complete for MVP)

```
Authentication          Workflow Execution
User Management         Approvals + Escalation
Assistant Management    Tool Framework
Semantic Kernel         REST Connector
Elsa Integration        Knowledge Search (pgvector)
Audit Logs              Operations Dashboard
Docker Deployment       Backup/Restore
```

### P1 (Important)

```
Email Connector         Database Connector
Document Versioning     Advanced Monitoring
Permission Matrix       Tenant Configuration
```

### P2 (Later)

```
Advanced Analytics      Additional Connectors
AI Optimization         Multi-Tenant Enhancements
Scheduled Workflows     Chatbot Widget Embedding
```

---

## Delivery Estimate

> Estimates from 2026-06-23 for remaining work to reach each milestone.

| Team Size | MVP Feature-Complete | Production Ready | Enterprise V1 |
|---|---|---|---|
| 1 Developer | ~12 weeks (Sep 2026) | ~15 weeks (Oct 2026) | ~20 weeks (Nov 2026) |
| 3 Developers | ~5 weeks (Jul 2026) | ~8 weeks (Aug 2026) | ~13 weeks (Sep 2026) |
| 5 Developers | ~3 weeks (Jul 2026) | ~6 weeks (Aug 2026) | ~10 weeks (Sep 2026) |

See [docs/COMPLETE_ROADMAP.md](docs/COMPLETE_ROADMAP.md) for sprint-level breakdowns.

---

## MVP Success Criteria

A business user can:

1. Login (JWT + Entra ID)
2. Create an AI Assistant (5 domain types)
3. Upload knowledge documents (PDF, DOCX, XLSX, PPTX)
4. Create a workflow with approval steps
5. Add approval policies with escalation
6. Connect an external API (REST connector)
7. Publish assistant for use
8. Ask assistant to perform work (streaming chat with RAG)
9. Trigger workflow execution
10. Approve or reject a request
11. Monitor workflow execution in real-time
12. View and filter audit logs

If all 12 work end-to-end, **R2WAI MVP is complete and production-ready.**
