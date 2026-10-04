# R2WAI 2.0 — Revised Product Roadmap
**Revision date:** 29 September 2026  
**Status:** Architecture-aligned working roadmap  
**Base document:** Existing R2WAI Product Roadmap, including its implementation notes and dated adoption status.

> **Revision note:** This revision retains the source roadmap's application-centric government platform vision and its factual implementation snapshot. It updates the target architecture and sequencing to reflect the R2WAI 2.0 direction: secure self-hosted AI agent execution, Microsoft Agent Framework as the target runtime, MCP as a governed integration layer, and a durable execution ledger in place of Elsa as the long-term workflow product. The existing codebase is to be migrated incrementally; this document does not claim that target components are already implemented.

---

## 1. Executive direction

R2WAI 2.0 is a self-hosted, application-centric enterprise AI Agent Platform for government and regulated organizations. It connects existing business applications and APIs, discovers available capabilities, and helps administrators configure, test, govern, and publish assistants and agent-powered workflows without building a separate AI application for every use case.

**Product principle:** Application first; agent execution is governed; data remains under the deploying organization's control.

For each connected application, R2WAI manages its assistants/agents, authorized knowledge, tools and MCP connections, deployment channels, policies, execution history, and operational visibility.

### Non-negotiable design principles

1. **Self-hosted and sovereign by default.** Support on-premises and offline/air-gapped deployments where the selected model and supporting components permit it. No silent cloud fallback or outbound data transfer.
2. **The model is not an authority.** Models propose tool calls; R2WAI authenticates the caller and enforces tenant, department, application, role, record, action, risk, and approval policies before execution.
3. **One governed integration path.** API tools, MCP tools, and platform capabilities use a shared authorization, policy, credential, audit, timeout, and execution boundary.
4. **Separate knowledge from live transactions.** Documents and policies are retrieved through permission-aware RAG. Current records and transactions are accessed only through authorized tools/APIs.
5. **Human accountability for consequential actions.** Require confirmation or human approval based on tool risk and policy; record decisions and outcomes.
6. **Incremental modernization.** Preserve working modules, data, API contracts, and tenant data where possible. Use additive migrations, compatibility adapters, feature flags, and regression tests. Do not perform a destructive rewrite.
7. **No simulated production capability.** Health, usage, evaluation, execution status, and audit data must come from real persisted events or actual probes. Label estimates and unsupported features clearly.

---

## 2. Target product experience

### Primary navigation

| Navigation | Purpose |
|---|---|
| Home | Tenant/application overview, setup checklist, recent activity and operational status |
| Applications | Register and manage business applications; discover APIs and configure application-scoped AI capabilities |
| Agents | Create, configure, test, version, and publish assistants/agents |
| Connections | Govern API, MCP, model, identity, and other external-system connections |
| Deployments | Publish to approved channels such as embedded widget and application-facing interfaces |
| Activity | Execution ledger, tool calls, approvals, policy decisions, audit, and troubleshooting |
| Settings | Users/roles, tenant and department settings, security, model policy, retention, and platform configuration |

This is a **navigation consolidation**, not a backend deletion plan. Existing knowledge, APIs, workflows, approvals, tools, policies, monitoring, and governance modules should be surfaced in the relevant application, agent, connection, deployment, activity, or settings context. Keep domain boundaries and existing records unless a reviewed migration explicitly replaces them.

### Core onboarding journey

**Connect → Discover → Configure → Review → Test → Publish → Monitor**

- Connect an application using a supported OpenAPI/Swagger definition, MCP server, or explicitly supported connection type.
- Discover endpoints, schemas, authentication requirements, and candidate capabilities. Treat discovered content as untrusted input.
- Generate a *draft* configuration: suggested tools, knowledge sources, access scopes, and assistant instructions. Never automatically activate discovered write-capable tools.
- Review permissions, risk classification, data handling, and approval requirements.
- Test using authorized test identities and representative cases.
- Publish an immutable, versioned configuration after authorization and policy validation.
- Monitor real executions, denials, approvals, model usage, and failures.

Automatic discovery may assist configuration but must not grant permissions or publish actions without review.

---

## 3. Target architecture and technology decisions

### Runtime and orchestration

| Concern | R2WAI 2.0 target | Migration rule |
|---|---|---|
| Agent runtime | Microsoft Agent Framework for new agent execution and orchestration | Introduce behind an R2WAI runtime abstraction; map current Semantic Kernel features and migrate incrementally |
| Existing AI functionality | Semantic Kernel remains during transition | Do not remove packages or plugins until all dependencies are inventoried, migrated, and regression-tested |
| Durable business process | R2WAI-owned durable execution ledger and worker/orchestration services | Replace Elsa as the long-term workflow product; do not build new user-facing Elsa designer functionality |
| Human approvals | R2WAI approval records, tasks, decisions, deadlines, escalation, and audit | Preserve and migrate existing approval history and behavior where feasible |
| Tool integration | Governed MCP and API tool layer | All tool execution passes through the same authorization and policy enforcement boundary |
| Knowledge | PostgreSQL/pgvector-based RAG, subject to repository verification | Preserve existing indexing and retrieval where valid; add ACL-aware retrieval and evaluation |
| Model providers | Provider abstraction for configured local and approved remote models | No implicit provider switching, external fallback, or model use outside policy |
| Deployment | Docker Compose self-hosted baseline; validate existing Kubernetes assets before production claims | Air-gapped mode must document images, model artifacts, dependencies, and update process |

### Microsoft Agent Framework migration

Before implementation, inspect the repository and official package/API compatibility for the exact supported .NET versions and Microsoft Agent Framework release. Do not assume that samples, preview APIs, or Semantic Kernel abstractions are interchangeable.

Required migration work:

1. Inventory every Semantic Kernel plugin, filter, prompt, memory integration, streaming path, and tool invocation.
2. Define runtime-neutral R2WAI contracts for agent definition, execution request, tool invocation, streaming events, cancellation, and result persistence.
3. Implement an Agent Framework adapter and retain the current runtime adapter during transition.
4. Port one representative read-only assistant and one controlled tool flow first.
5. Compare output behavior, tool authorization, streaming, cancellation, error handling, usage accounting, and audit records.
6. Migrate by feature flag and tenant/application cohort; maintain rollback until parity is demonstrated.
7. Remove legacy runtime dependencies only after repository-wide dependency analysis and successful regression, integration, and security tests.

### Durable execution ledger (long-term Elsa replacement)

The platform should own a durable execution model rather than expose a separate workflow-design product. Agent Framework handles agent reasoning and tool orchestration; the R2WAI execution service owns persistence, state transitions, authorization checkpoints, retries, resumability, and human task boundaries.

Minimum execution concepts:

- Execution, execution step, event, tool invocation, approval task, policy decision, artifact, and correlation identifiers.
- Explicit state machine with validated transitions and optimistic concurrency.
- Durable checkpoints and resumable execution after process restart.
- Cancellation, deadlines, bounded retries, backoff, idempotency keys, and duplicate-delivery protection.
- Human approval/denial with actor, timestamp, comments, policy basis, and audit trail.
- Per-tenant and per-application isolation and authorization on every read and mutation.
- Versioned execution definitions and immutable published agent/configuration versions.
- Failure, timeout, compensation, and manual recovery states; no false “success” status.

Do not begin by recreating a generic visual workflow designer. Prioritize reliable execution primitives and a small set of governed, reusable patterns. Add a visual low-code experience only after execution semantics, security, and recovery are proven.

### MCP and API connection architecture

MCP is a first-class integration protocol, not a bypass around the API/tool gateway.

Required controls for every MCP/API connection:

- Tenant/application ownership and explicit connection allowlisting.
- Authenticated server identity where supported; approved transport and endpoint configuration.
- Secrets stored through the configured secret-protection mechanism; never returned to the client or model.
- Tool discovery creates inactive drafts. An administrator reviews schemas, data classification, risk, permissions, and side effects before activation.
- Caller identity and authorization context propagated or securely mapped; no ambient shared privilege.
- Egress allowlists, DNS/IP validation, private-network and metadata-service protections, and SSRF defenses.
- Request/response size limits, timeouts, cancellation, rate limits, concurrency limits, and bounded retries.
- Idempotency or duplicate-action protection for writes; confirmation/approval for high-risk actions.
- Redaction of credentials and sensitive payloads from logs; audit of tool selection, policy decision, execution result, and actor.
- Schema validation and safe handling of untrusted tool output, including prompt-injection attempts.
- Explicit connection health checks that perform real probes and report probe time and result.

---

## 4. Security, identity, and governance

### Authorization model

Implement authorization in the backend, not merely in navigation or UI controls.

- **RBAC:** platform and tenant roles determine broad capabilities.
- **ABAC/scoped access:** evaluate tenant, department, application, jurisdiction, record ownership, assignment, data classification, requested action, and policy context.
- Apply authorization to API endpoints, queries, knowledge retrieval, agent execution, connection access, tool invocation, approval actions, execution history, and exports.
- Enforce tenant and resource scope in database queries and service boundaries; test cross-tenant and cross-department access explicitly.
- Anonymous/public widget access is a separate unauthenticated context, not a privileged role. It must only expose explicitly published public knowledge, navigation, and tools.
- Never trust role, tenant, department, jurisdiction, record ownership, or approval claims supplied by the model or arbitrary client payloads.

### Policy evaluation order

Every tool execution must pass a deterministic server-side decision pipeline:

1. Authenticate the requesting identity or establish the restricted anonymous context.
2. Resolve tenant, department, application, agent version, and connection.
3. Validate tool is published, enabled, and bound to the expected application/connection.
4. Evaluate RBAC and resource/action ABAC scopes.
5. Evaluate data, model, tool, risk, and outbound-network policies.
6. Determine whether user confirmation or human approval is required and verify it is valid for this exact action.
7. Validate and execute through the controlled gateway.
8. Persist decision and outcome audit records, with sensitive data redacted.

Default to deny when identity, scope, policy, approval, or connection state cannot be established. Policy evaluation failures must not silently allow execution.

### AI governance

- Platform/Super Admin manages the approved model/provider registry and global security baselines.
- Department/Application administrators select only from models and capabilities approved for their scope.
- Record model/provider, model configuration, prompt/agent version, retrieved source references, tool calls, policy decisions, and usage where available.
- Version and review system prompts, agent instructions, tool schemas, and published configuration.
- Treat retrieved documents, user input, and tool output as untrusted; defend against prompt injection and data exfiltration.
- Do not expose private chain-of-thought. Provide concise explanations, citations, tool/action summaries, and policy reasons suitable for audit.
- Apply data minimization, retention, deletion, and export policies to conversations, knowledge, execution events, and telemetry.

---

## 5. Knowledge and Agentic RAG

### Standard RAG (baseline)

- Support the currently implemented document ingestion formats only after verifying actual code and test coverage.
- Persist source identity, tenant/application scope, document version, access metadata, indexing status, and errors.
- Enforce document-level ACL and scope filters at retrieval time, not only at upload time.
- Provide source citations and distinguish retrieved knowledge from live API results.
- Track indexing and embedding failures accurately; do not report a source as searchable until indexing is complete.
- Test retrieval quality using a versioned evaluation set and report the metric definition, sample size, and date.

### Agentic RAG (controlled advanced capability)

Agentic RAG is an optional retrieval strategy, not an unrestricted agent loop. It may use query rewriting, bounded multi-query retrieval, hybrid search, reranking, evidence sufficiency checks, and a limited retrieval iteration budget.

Required safeguards:

- Apply identity and document ACL filters to every retrieval attempt.
- Bound iterations, latency, token usage, result count, and cost where applicable.
- Cite evidence and indicate when evidence is insufficient; do not fabricate citations.
- Do not permit retrieval strategy to invoke transactional tools or expand the caller's permissions.
- Defend against instructions embedded in retrieved documents.
- Fall back to standard RAG or a clear no-answer/handoff when evidence is insufficient or limits are reached.
- Evaluate answer grounding, retrieval relevance, unauthorized retrieval, and refusal behavior before enabling for production applications.

---

## 6. Model provider strategy and BitNet.cpp

Provide a provider abstraction so model hosting is replaceable without changing agent, policy, or application domain logic.

**BitNet.cpp is an optional local inference provider, not a mandatory platform dependency.** Before committing to it for production, validate its official serving interface, supported model formats, hardware/OS requirements, concurrency behavior, streaming support, and operational maturity against the actual deployment target.

Implementation requirements:

- Build a proof of concept behind the same provider contract used by other approved local models.
- Verify whether an adapter or serving wrapper is required; do not assume it exposes an OpenAI-compatible API.
- Measure latency, throughput, memory/CPU requirements, context limits, and answer quality on representative government tasks.
- Make model capabilities and limitations visible in administration and deployment documentation.
- Do not silently route to a cloud provider if local inference is unavailable.
- Keep provider-specific settings out of business logic and enforce model allowlists centrally.

---

## 7. Revised delivery plan

The source roadmap's dated implementation notes remain historical evidence. The sequence below is the recommended R2WAI 2.0 delivery order. Schedule estimates should be re-baselined after a repository and dependency audit; no duration is guaranteed.

### Phase 0 — Baseline, safety, and migration design

**Goal:** Establish a verified baseline before large architecture changes.

Deliverables:
- Run the current build, test suites, migrations, and security checks; record exact commands, results, and environment.
- Inventory entities, API contracts, frontend routes, Semantic Kernel usage, Elsa usage, MCP/API tools, background services, and deployment assets.
- Verify current Department/Application adoption, RBAC role set, tenant filters, widget behavior, approval lifecycle, and database migration status.
- Create a risk register and traceability matrix linking known gaps to tests and owners.
- Define runtime abstraction, execution-ledger schema, migration/backfill approach, and rollback strategy.
- Resolve critical authorization, cross-tenant, PII, secret-handling, and production-configuration defects before enabling new write-capable agent flows.

**Exit criteria:** Reproducible baseline; architecture and migration decisions reviewed; critical security gaps either closed or explicitly block production release.

### Phase 1 — Secure application and connection foundation

**Goal:** Make application ownership and governed connections reliable.

Deliverables:
- Verify Department/Application entity relationships, migrations, and tenant-scoped query behavior.
- Complete application-to-connection-to-tool association and lifecycle.
- Harden OpenAPI discovery and introduce MCP server discovery/registration as a draft-only process.
- Implement connection secrets handling, egress restrictions, endpoint validation, schema validation, and connection health probes.
- Establish a single server-side tool execution boundary for existing API tools and new MCP tools.
- Complete role and attribute-based authorization enforcement across API, knowledge, agent, tool, approval, and activity paths.

**Exit criteria:** A discovered tool remains inactive until reviewed; an authorized test identity can execute an approved low-risk tool through the gateway; unauthorized attempts are denied and audited.

### Phase 2 — Agent Framework runtime foundation

**Goal:** Introduce Microsoft Agent Framework without destabilizing existing assistants.

Deliverables:
- Implement runtime-neutral agent and execution contracts.
- Add Microsoft Agent Framework adapter after verifying package/API compatibility.
- Retain Semantic Kernel adapter for existing features during transition.
- Support streaming, cancellation, bounded execution, tool-call mediation, error handling, and persisted execution correlation.
- Migrate a read-only assistant and a controlled tool-use scenario behind feature flags.
- Compare parity, latency, usage reporting, audit coverage, and authorization behavior.

**Exit criteria:** Both runtime paths pass the same security and behavioral contract tests; rollout and rollback are demonstrated.

### Phase 3 — Durable execution ledger and approvals

**Goal:** Establish durable execution and human accountability, replacing Elsa as the long-term workflow product.

Deliverables:
- Implement execution, step, event, approval task, and tool invocation persistence.
- Add state transitions, optimistic concurrency, checkpointing, resumability, cancellation, timeouts, idempotency, and bounded retry policies.
- Migrate required approval lifecycle behavior, history, escalation, and comments.
- Add an Activity view for execution timelines, approval queues, failures, and policy decisions.
- Create migration adapters or export/import tooling for existing Elsa definitions and active instances where feasible; document anything that cannot be migrated.
- Keep existing Elsa runtime available only as a controlled compatibility path until active processes are safely drained or migrated.

**Exit criteria:** A multi-step execution survives service restart, enforces approval gates, prevents duplicate writes, and produces a complete auditable timeline.

### Phase 4 — Agent Studio, RAG, and safe self-service

**Goal:** Deliver the core administrator experience on top of secure runtime foundations.

Deliverables:
- Consolidate assistant/agent create, configure, test, version, and publish workflows.
- Provide the onboarding journey: Connect → Discover → Configure → Review → Test → Publish.
- Generate draft tools, knowledge configuration, and agent instructions with explicit review.
- Implement ACL-aware standard RAG, citations, source lifecycle, and evaluation.
- Add bounded Agentic RAG as an opt-in capability after standard RAG and authorization tests pass.
- Keep advanced configuration available but hidden behind clear controls; expose risk and permission settings in the review step.

**Exit criteria:** A department administrator can configure and test a scoped application assistant using approved defaults, understand unresolved warnings, and publish only after required reviews.

### Phase 5 — Deployment channels and public/officer scope

**Goal:** Publish assistants safely to approved audiences.

Deliverables:
- Bind deployments to immutable agent/configuration versions and explicit application/channel identifiers.
- Rework the embedded widget to resolve a published application deployment and enforce anonymous/public restrictions server-side.
- Enforce authenticated User and Officer/Admin scopes for private knowledge and transactional tools according to the configured authorization model.
- Add deployment health, version rollback, and channel-specific policy checks.
- Test anonymous, user, officer, department-admin, and super-admin access paths, including direct API calls.

**Exit criteria:** Public visitors cannot access private knowledge or tools; authenticated users only access permitted records and actions; published versions can be rolled back.

### Phase 6 — Governance, evaluation, and operations

**Goal:** Make production behavior observable and governable.

Deliverables:
- Approved model/provider registry and configuration inheritance across global, tenant, department, application, and agent scopes.
- Audit events for model use, retrieval, tool calls, policy decisions, approvals, configuration changes, and publication.
- Accurate persisted usage accounting, execution metrics, health probes, and failure reporting.
- Evaluation suites for RAG grounding, tool selection, policy enforcement, prompt injection, and access isolation.
- Audit filtering/export with permission checks and redaction.
- Operational alerts, retention controls, backup/restore procedures, and incident runbooks.

**Exit criteria:** Operators can answer who initiated an execution, which published agent/model/tools were involved, what policies applied, what approvals occurred, and how the execution ended.

### Phase 7 — Production hardening and government pilot

**Goal:** Validate one real application end-to-end in the intended deployment environment.

Deliverables:
- Threat modeling and independent security review of identity, gateway, MCP, RAG, agent runtime, and execution ledger.
- Load, soak, resilience, restart, recovery, and tenant-isolation tests under representative pilot conditions.
- Validate Docker Compose and existing Kubernetes assets separately; do not describe Kubernetes as production-ready until tested.
- Validate offline/air-gapped installation, model provisioning, image/artifact inventory, backups, upgrades, and recovery if required by the pilot.
- Prepare department-admin training, deployment, operations, incident response, and support runbooks.
- Conduct a pilot with a limited set of read-only tools first, then introduce write-capable actions only after explicit security approval.

**Exit criteria:** All pilot acceptance scenarios pass in the target environment, with evidence retained and critical/high security findings resolved or formally blocked from release.

### Post-pilot

- Multi-agent collaboration, only after single-agent governance and execution reliability are proven.
- Additional channels and reusable application templates.
- Kubernetes scaling and multi-replica validation; configure SignalR backplane only if the deployed topology requires it.
- Additional telemetry integrations such as Prometheus/Grafana where justified by operations requirements.
- Advanced scheduling and inbound webhook triggers with authentication, replay protection, and policy enforcement.
- Marketplace and cross-department template distribution with signing, versioning, and approval controls.

---

## 8. Pilot acceptance criteria

The pilot should demonstrate, with recorded test evidence, that an administrator can:

1. Register an existing application and establish a governed connection.
2. Discover OpenAPI endpoints and/or approved MCP tools without activating them automatically.
3. Review and configure an application-scoped assistant/agent.
4. Use permission-filtered knowledge retrieval with verifiable citations.
5. Test a read-only tool call through the gateway under a real authorized identity.
6. Demonstrate denial and audit for unauthorized or out-of-scope tool calls.
7. Execute a multi-step durable task with a human approval gate and restart recovery.
8. Publish a versioned assistant to an approved channel and roll back to a previous version.
9. Verify that anonymous/public, authenticated user, and officer/admin contexts cannot cross their authorized data scopes.
10. Inspect a persisted execution timeline, policy decisions, approvals, tool outcomes, and accurate usage/health data.

A pilot is not considered successful merely because the UI is complete or a demo conversation works. The above scenarios must pass end-to-end in the intended deployment environment.

---

## 9. Delivery and estimation policy

The source roadmap contains estimates of approximately 21 weeks for one developer, 8 weeks for three developers, and 5 weeks for five developers for its earlier phase scope. Those estimates should **not** be treated as estimates for this revised R2WAI 2.0 scope: the runtime migration, durable execution ledger, MCP governance, and production validation materially change the work.

Re-estimate after Phase 0 using:
- verified remaining work and test baseline;
- team experience with .NET, Agent Framework, security, and distributed execution;
- required on-premises/offline constraints;
- migration volume for existing Elsa workflows and persisted instances;
- pilot scope, target hardware, and acceptance evidence requirements.

Do not trade away authorization, auditability, recovery, or tenant isolation to meet a calendar estimate.

---

## 10. Source roadmap status and traceability

The material below is retained from the source roadmap for traceability. Its historical implementation statements, dates, and test counts are not independently re-verified by this revision. Where the source roadmap conflicts with the revised target above, the revised target architecture and delivery plan take precedence.

**# R2WAI Product Roadmap**

\> Government AI Application Platform

**---**

**## Product Vision**

R2WAI (Request To Work AI) is a self-hosted platform where government agencies connect their **\*\*existing applications\*\*** and R2WAI automatically discovers, configures, and delivers an **\*\*AI assistant\*\*** for each one — with knowledge (RAG), tools (API gateway), workflows, approvals, and governance — without writing code.

The core idea: **\*\*Application first.\*\*** R2WAI is not a collection of independent AI features; it is an application-centric platform. Connect a Property Tax app → get a Property Assistant. Connect the Revenue app → get a Revenue Assistant. The assistant, its knowledge, tools, permissions, and workflows all belong to that application.

The platform is built on .NET 10 with Semantic Kernel and Elsa workflow engine, supports multiple AI models (OpenAI, Azure OpenAI, Ollama), and provides full data sovereignty through on-premise deployment. It separates **\*\*RAG\*\*** (FAQ / policy / documents) from **\*\*live data\*\*** (status / transactions via APIs), and it **\*\*never lets the AI model call a government API directly\*\*** — every tool call passes through a secure, policy-enforced Tool/API Gateway with RBAC + ABAC.

**---**

**## Strategic Pivot**

**### From**

\`\`\`text

Tenant

 ├── Assistant

 ├── Chatbot

 ├── KnowledgeBase

 ├── Workflow

 └── Model

\`\`\`

**### To**

\`\`\`text

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

\`\`\`

**### Why**

\| Change | Reason |

\|---|---|

\| **\*\*Application is the central entity\*\*** | Government deployments are organized around business systems, not around AI features. Onboarding = "connect this app," not "build a chatbot." |

\| **\*\*Auto Discovery + Low-Code Wizard\*\*** | Connects an existing app → understands it → generates configuration. Admin only reviews, tests, publishes. |

\| **\*\*Tool/API Gateway (RBAC + ABAC + policies)\*\*** | The LLM must never directly call government APIs. Every tool needs role, permission, risk level, confirmation, approval, and audit metadata. |

\| **\*\*Remove duplicated concepts\*\*** | \`Chatbot\` merges into \`Assistant → Channels\`. \`Agent\` / \`Copilot\` / \`AI Employee\` collapse into \`Assistant\`. |

\| **\*\*Remove standalone studios\*\*** | Chatbot/Model/Integration/Navigation/Tool/Media studios fold into application areas or Administration. |

\| **\*\*Separate RAG from live data\*\*** | Policy/documents → RAG; current status/transactions → API. Combined by the assistant, governed differently. |

\| **\*\*AI Governance\*\*** | Super Admin approves models; Department Admin selects from the approved set. |

See [ARCHITECTURE.md]\(ARCHITECTURE.md) for the full target architecture and component action register, and [docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md]\(docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md) for the code-ready implementation plan (phases, entities, gateway/risk specs, acceptance criteria).

**---**

**## Target Personas**

\| Persona | Role | Pain Point | How R2WAI Helps |

\|---|---|---|---|

\| **\*\*Super Admin\*\*** | Governs the platform | No control over which AI models are used or where data goes; no enforcement of AI policy | Global/model/security governance: approved model registry, model/prompt/tool/data/risk policies, full audit |

\| **\*\*Department Admin\*\*** | Manages applications and assistants for one department | Building AI for each system requires consultants and weeks of work | Connect an application → auto-discovery → auto-configuration → review → test → publish in hours |

\| **\*\*Officer\*\*** | Does the work (cases, approvals, jurisdiction) | Scattered systems; no unified view of assigned cases or authorized actions | Assistant grounded in internal knowledge and authorized workflows, scoped to department/jurisdiction/assigned cases |

\| **\*\*Citizen / Public\*\*** | Anonymous website visitor | Can't find answers on public websites; gets lost in navigation | Public FAQ / RAG / navigation via the floating widget, limited to public data only |

\| **\*\*IT Admin\*\*** | Deploys and manages the platform | AI tools require sending government data to third-party clouds | Self-hosted deployment, multi-model support (Ollama for air-gapped), tenant/department isolation |

**---**

**## Success Metrics**

\| Metric | Pilot Target | Scale Target |

\|---|---|---|

\| Time to first working assistant | < 1 hour (discovery + review) | < 30 minutes |

\| Approval cycle time | Same-day | < 2 hours (with SLA tracking) |

\| Knowledge query accuracy (top-5 recall) | > 70% | > 85% (hybrid search) |

\| Tool gateway policy violations caught | 100% (blocked before execution) | 100% |

\| Concurrent users supported | 50 | 500+ |

\| Deployment time from scratch | < 30 minutes | < 15 minutes (K8s, later) |

\| System uptime | 95% | 99.5% |

**---**

**## Current State Snapshot**

\> Verified against the codebase 2026-06-23; security hardening pass completed 2026-07-14 (see [ENTERPRISE_AUDIT_REPORT.md]\(ENTERPRISE_AUDIT_REPORT.md)). **\*\*This snapshot predates the Department/Application entities (added 2026-08-10) and the 2026-08-20 UI/UX redesign pass\*\*** — see [ARCHITECTURE.md]\(ARCHITECTURE.md#adoption-status) for current adoption status. That pass delivered role-based navigation (5 personas via \`RolePersona\`, UI-only — see README's [UI/UX & Role-Based Navigation]\(README.md#uiux--role-based-navigation) section), the Automations rename + wizard + simple detail/edit view, an Assistant Studio simple-view/edit split, an Integrations connection-test action, and closed a real gap where no role could ever be assigned to a user after creation. It does **\*\*not\*\*** touch Phases 1–2, 6 (Discovery Engine, Tool/API Gateway, AI Governance) below, which remain \`[target]\`.

**### What exists and works**

\- ASP.NET Core 10 + Clean Architecture (4 layers), React SPA (TypeScript + MUI), PostgreSQL 16 + pgvector, Docker Compose

\- JWT auth (15-min access + 7-day refresh rotation), Entra ID SSO, TOTP MFA, API-key scheme

\- Tenant isolation via EF Core global query filters; audit log auto-populated on mutation

\- AI assistant studio with 4 Semantic Kernel plugins (RAG, Document, Workflow, Assistant), streaming chat, citations

\- RAG pipeline for file uploads (PDF/DOCX/XLSX/PPTX → extract → chunk → embed → pgvector search)

\- Elsa workflow engine with approval steps, approval lifecycle with escalation background service

\- Tool framework (\`ITool\`, \`ToolRegistry\`, \`HttpTool\`, \`EmailTool\`), integration marketplace UI

\- Embeddable chatbot widget (anonymous visitors: tools disabled by design)

\- Operations center: monitoring dashboard, audit viewer, health checks, OpenTelemetry, Serilog

\- Test suite: full solution green (fast unit suites + integration/flow tests + Playwright e2e); exact counts drift too fast to pin a number here — see CI

**### Known gaps to close before new features (P0)**

1\. Database migrations for the new entity model

2\. Real frontend ↔ backend integration (remove mock-data paths)

3\. \~\~Real KB indexing / embeddings (text/URL source indexing is broken)\~\~ — closed; see [ARCHITECTURE.md]\(ARCHITECTURE.md#adoption-status)

4\. Workflow step chaining

5\. Real approval UI

6\. \~\~Tenant isolation verification across new entities\~\~ — closed; \`e2e/cross-tenant-isolation.spec.ts\` verifies at the API level

7\. RBAC enforcement review + ABAC introduction — enforcement review done (role collapse migration, Integrations gateway-bypass fix below); ABAC itself remains open

8\. API integration (Application → API → Tool pipeline)

9\. \~\~Security hardening (concurrency tokens, gateway controls)\~\~ — closed; \`xmin\`-based optimistic concurrency on publish paths, \`IntegrationsController.Test\` governance-bypass fix, \`IEnabledToolScope\` defense-in-depth check

10\. Production configuration validation

\> As of 2026-08-29, items 3/6/9 above are closed — see [ARCHITECTURE.md#adoption-status]\(ARCHITECTURE.md#adoption-status) for the current, maintained list of what's implemented vs. still \`[target]\`; this roadmap's own gap list is not kept in lockstep with every change.

\> **\*\*2026-08-20 addendum — Tool/API Gateway (item 9) status:\*\*** \`AiFunctionAuditFilter\` now enforces

\> \`RequiredRole\`/\`ApprovalRequired\` on every Semantic Kernel tool call and writes a real \`AuditLog\` row,

\> verified end-to-end against a live LLM (denial correctly blocks \`start_workflow\`, audit row persists

\> with valid JSON metadata, the assistant's denial reply reaches the caller as a normal 200 response).

\> **\*\*2026-08-21 — the concurrency gap above is root-caused and fixed.\*\*** Reproduced deterministically

\> (no live LLM needed — \`ChatConcurrencyRegressionTests.cs\` drives \`AiFunctionAuditFilter\` through a

\> real, LLM-free Semantic Kernel function invocation against a Testcontainers Postgres) and confirmed via

\> \`EnableSensitiveDataLogging\` + full SQL trace: \`AiFunctionAuditFilter\`'s denial-audit write shared the

\> chat request's own \`DbContext\`, so its mid-call \`SaveChanges()\` flushed the request's pending

\> conversation/message entities early. Fix: the filter now writes its \`AuditLog\` through an isolated

\> \`IServiceScopeFactory\`-created scope (\`AiFunctionAuditFilter.WriteAuditAsync\`), decoupling the audit

\> side-effect from the caller's unit of work entirely. That's the complete, minimal fix.

\>

\> Three other changes drafted alongside that fix turned out to be unnecessary, and one was actively

\> harmful — removed after the isolated-scope fix alone was confirmed sufficient (test passes with

\> *\*only\** that change in place):

\> - An "early save" of the user's message in \`ChatWithAssistantCommandHandler\`, added to shrink the

\>   window where entities sit uncommitted — **\*\*this introduced a second, different bug\*\***: once the

\>   conversation is saved and transitions from \`Added\` to \`Unchanged\`, EF Core can no longer infer that

\>   a message added afterward via \`conversation.AddMessage()\` (discovered only through collection-

\>   navigation fixup, never explicitly \`Add()\`-ed) is new. It generates an \`UPDATE\` instead of an

\>   \`INSERT\`, matches zero rows, and throws the same \`DbUpdateConcurrencyException\` — just for a

\>   different entity, and reproducible without any audit write in play at all. Reverted.

\> - \`UnitOfWork.SaveChangesAsync\`'s retry-with-detach loop on \`DbUpdateConcurrencyException\` — a

\>   defensive safety net that silently detaches (drops) non-\`Added\` conflicting entries and retries.

\>   With the real cause fixed at the source, this masks data loss on any future concurrency bug rather

\>   than surfacing it. Reverted; a genuine conflict should fail loudly, not vanish.

\> - \`MaxBatchSize(1)\` on the Npgsql connection (disables statement batching app-wide) — a diagnostic aid

\>   from narrowing this down that never actually changed the failure (confirmed by the original

\>   debugging session and re-confirmed here). Reverted.

\>

\> \*\*2026-08-22 — the "second, different bug" above was not just a draft-change artifact; it was already

\> live, unconditionally, in real production code paths.\*\* Building a live-DB regression test for Context

\> & Memory (\`ContextMemorySummarizationRegressionTests.cs\`, Testcontainers Postgres) hit the exact

\> \`DbUpdateConcurrencyException\` described in the 2026-08-21 note above — but from \`ChatService

\> .SendMessageAsync\`, which \*always\* reloads the conversation via \`FirstOrDefaultAsync\` before calling

\> \`conversation.AddMessage()\` (there is no "new conversation, still \`Added\`" branch there at all). The

\> same pattern existed in \`ChatWithAssistantCommandHandler\`'s *\*existing\**-conversation branch

\> (\`command.ConversationId.HasValue\`) — only its *\*new\**-conversation branch was exercised by

\> \`ChatConcurrencyRegressionTests.cs\`, so the existing-conversation path was never covered by any

\> Postgres-backed test. Every follow-up message in an already-persisted conversation was one Guid-keyed

\> \`AddMessage()\` away from silently failing to save. This was invisible in the fast test suite because

\> those tests run against the EF InMemory provider, which doesn't enforce real affected-row-count

\> semantics the same way Npgsql does. Fix: at all four call sites (\`ChatService.cs\` ×2,

\> \`ChatWithAssistantCommand.cs\` ×2 — \`SendMessageCommand.cs\` already did this correctly and was the

\> template), explicitly \`Add()\` the message returned by \`conversation.AddMessage()\` instead of relying on

\> EF's graph-fixup state inference, which cannot tell a new entity with a client-set Guid key from an

\> existing one once its parent is no longer in the \`Added\` state. Verified via the new regression test

\> (both summarization-enabled and -disabled variants pass) plus a full non-UI test run: 593/593 passed.

**---**

**## Roadmap — Migration to the Application-centric Platform**

**### Phase 0 — Architecture Consolidation (Weeks 1-3)**

**\*\*Goal:\*\*** Restructure the domain and UI around \`Application\`; remove duplication. No new user-facing features.

**#### Deliverables**

\- Introduce \`Department\` and \`Application\` entities; re-home Assistant, Knowledge, Tools, Workflows, Policies, APIs under \`Application\`

\- Merge \`Chatbot\` into \`Assistant → Channels\` (migration + data backfill)

\- Collapse Assistant/Agent/Chatbot concepts into a single \`Assistant\` with capabilities: Knowledge, Tools, Workflow, Model, Policies, Channels

\- Remove standalone Chatbot / Model / Integration / Navigation / Tool / Media studios from primary UX; keep a central connector registry internally

\- Remove Qdrant from the stack (pgvector only)

\- Close the P0 technical gaps (see above): migrations, mock-data removal, KB indexing, workflow chaining, approval UI, tenant isolation verification, RBAC hardening, config validation

**\*\*Exit criteria:\*\*** new domain model compiles with migrations; no separate chatbot entity; studio navigation reduced to 4 primary areas.

**---**

**### Phase 1 — Application Studio + Discovery Engine (Weeks 4-6)**

**\*\*Goal:\*\*** Make Application Studio the main product and build auto-discovery.

**#### Deliverables**

\- Application Studio UI: Applications list + per-application Overview / Discovery / APIs / Knowledge / Navigation / Assistant / Tools / Workflows / Security / Policies / Testing / Monitoring / Publish

\- Application Discovery Engine: OpenAPI/Swagger discovery, endpoint discovery, authentication detection, API schema analysis, navigation discovery, capability detection

\- Auto-configuration generators: tool suggestion, FAQ generation, permission suggestion, assistant configuration draft

\- Review workflow: admin reviews discovered configuration, fixes issues, re-runs discovery

**\*\*Exit criteria:\*\*** connecting an existing Swagger-enabled application produces a reviewable assistant configuration without manual API entry.

**---**

**### Phase 2 — Tool/API Gateway + Policy Engine (Weeks 7-10)**

**\*\*Goal:\*\*** The LLM never calls government APIs directly.

**#### Deliverables**

\- Tool/API Gateway: every tool call passes through authorization → policy → gateway → application API

\- Per-tool metadata: Application, Endpoint, HTTP Method, Role, Permission, Risk Level, Confirmation Required, Approval Required, Audit Required

\- Policy engine with **\*\*configuration inheritance\*\***: Global → Department → Application → Assistant → User Context

\- **\*\*RBAC + ABAC\*\***: authorization considers Role + Department + Application + Jurisdiction + Record ownership + Action + Policy

\- AI Governance foundation: Model Registry, Approved Models, Model Policy, Prompt Policy, Tool Policy, Data Policy, Risk Policy

\- Gateway audit logging for every invocation

**\*\*Exit criteria:\*\*** an approved user can execute an approved tool; any non-authorized or non-compliant tool call is blocked and audited; the AI model itself holds no permission authority.

**---**

**### Phase 3 — Adaptive Wizard + Default-Configured UX (Weeks 11-12)**

**\*\*Goal:\*\*** One-click onboarding with advanced config hidden by default.

**#### Deliverables**

\- Adaptive wizard flow: Connect → Auto Discover → Auto Configure → Review Issues → Test → Publish

\- Default screen shows a review checklist (\`✓ Connected ✓ API discovered ✓ Knowledge configured ...\`) with **\*\*[Test Assistant]\*\*** / **\*\*[Publish]\*\***

\- Advanced Configuration disclosure (Model, Prompt, Chunking, Embedding, Tool permissions, API timeout, RAG threshold, Security policy, Workflow settings)

\- Knowledge split UI: RAG sources (FAQ/policy/documents) vs live API data — never treated as the same thing

**\*\*Exit criteria:\*\*** a non-technical department admin can onboard an application end-to-end using defaults only.

**---**

**### Phase 4 — Security Model: Public / User / Officer (Weeks 13-14)**

**\*\*Goal:\*\*** Correct, scoped access for each audience.

**#### Deliverables**

\- Public (anonymous context, not a role): Public FAQ, Public RAG, Public Navigation, Public APIs via floating widget

\- User: own information, own applications, own documents, approved transactions

\- Officer: department, jurisdiction, assigned cases, internal knowledge, authorized workflows

\- Role hierarchy: Super Admin → Department Admin → Officer / User (Public is an anonymous context)

\- Floating widget re-architecture: Website → R2WAI Floating Widget → Application ID → R2WAI Gateway → Application Assistant

**\*\*Exit criteria:\*\*** each audience sees exactly its scope; anonymous visitors cannot reach any tool or internal knowledge.

\> **\*\*Partially delivered 2026-08-20, nav model simplified further 2026-08-29\*\*** (UI navigation only — the scoped *\*data\** access this phase also calls for, e.g. a User's "own applications" filtered API results, is not built): the original \`RolePersona\` gave Super Admin/Department Admin/Officer/Citizen/Public each their own nav menu; the separate "Officer" tier was since folded into Admin (it was already treated identically), leaving \`roleNav.ts\` with three real personas — Super Admin/Admin/User (+ Public) — and role assignment (previously impossible) works end-to-end. A later migration (\`20260829091112_CollapseRbacToThreeRoles\`) retired the DB role rows (\`WorkflowManager\`/\`Editor\`/\`Contributor\`/\`UserManager\`) the old Officer mapping depended on, so the DB role set now genuinely matches the 3-persona nav rather than being mapped down to it. What's still open: the ABAC/scoped-query layer and the floating-widget re-architecture.

**---**

**### Phase 5 — Workflow & Approvals Completion (Weeks 15-16)**

**\*\*Goal:\*\*** Complete the workflow/approval surface. **\*\*Superseded direction (see \`docs/audit/\`):\*\*** a later "AUDIT + TRANSFORMATION" brief reverses the "Elsa kept" premise below — Elsa is to be removed entirely (no workflow product/designer), replaced by a durable execution ledger + Microsoft Agent Framework. Elsa remains in place today pending that migration; this phase's deliverables are not yet superseded in code.

**#### Deliverables**

\- Workflow step chaining and branching (step output variable mapping)

\- Visual designer embedding / drag-and-drop step builder

\- Real approval UI: approve/reject/escalate with SLA timers, comments, history

\- Workflow templates library (department use cases: case intake, document request, payment approval)

\- Application-scoped workflow visibility in the Application Studio

**\*\*Exit criteria:\*\*** a multi-step workflow with human approvals runs end-to-end from the Studio and is fully audited.

**---**

**### Phase 6 — Operations & Governance Expansion (Weeks 17-18)**

**\*\*Goal:\*\*** Monitoring, audit, and governance at scale.

**#### Deliverables**

\- Gateway/invocation monitoring in Operations Center (per-application dashboards)

\- Audit expansion: tool invocations, policy decisions, model usage, permission changes

\- AI evaluation: assistant answer quality, RAG recall, tool misuse attempts

\- Audit log export (CSV/JSON), advanced filtering

\- Concurrency tokens on all entities

**\*\*Exit criteria:\*\*** every sensitive action is visible in Operations; governance reports answer "who did what, with which model, through which tool, when."

**---**

**### Phase 7 — Production Hardening & Pilot (Weeks 19-21)**

**\*\*Goal:\*\*** First government pilot deployment.

**#### Deliverables**

\- Production configuration validation across real environments

\- Load testing under pilot conditions

\- Security review of gateway/policy paths

\- Pilot runbook, training, and support runbook for department admins

\- Docker Compose production configuration finalized (Redis/MinIO only if required)

**\*\*Exit criteria:\*\*** pilot department onboarded with at least one live application assistant; success metrics met.

**---**

**### Post-Pilot (Later)**

\- Kubernetes deployment validation under real load — baseline manifests already exist under \`k8s/\` (namespace, ConfigMap, Redis, API Deployment+HPA, Studio Deployment, Ingress); this item is load-testing/validating that config, not building it from scratch

\- SignalR Redis backplane (Redis cache itself is already live — distributed rate-limiting, login lockout, and the AiUsage daily cap all run through it today; a SignalR backplane for multi-replica hub fan-out is the piece still unconfigured)

\- Scheduled workflows (cron triggers via Elsa.Scheduling), webhook inbound triggers

\- URL source crawling for knowledge bases

\- Advanced AI (multi-agent orchestration) — only after governance is proven

\- Prometheus + Grafana monitoring stack

See [docs/COMPLETE_ROADMAP.md]\(docs/COMPLETE_ROADMAP.md) for the legacy sprint plans, epic backlogs, and task-level breakdowns (pre-pivot).

**---**

**## Priority Matrix**

**### P0 (Application-Centric Foundation)**

\`\`\`

Application Entity + Migrations   Department Entity

Assistant + Channels (Chatbot merge)

Discovery Engine                  Auto-Config Generators

Tool/API Gateway                  RBAC + ABAC + Policies

Configuration Inheritance         AI Governance (Model Registry)

KB Indexing Complete              Workflow Chaining + Approval UI

Audit Expansion                   Frontend ↔ Backend Integration

\`\`\`

**### P1 (Important)**

\`\`\`

Adaptive Wizard                   Default-Hidden Advanced Config

Public/User/Officer Security      Floating Widget (Application ID)

Application Monitoring            Template Workflows

Audit Export                      \~\~Concurrency Tokens\~\~ (closed — xmin on publish paths)

\`\`\`

**### P2 (Later)**

\`\`\`

Kubernetes HPA validation         Redis wired (cache/rate-limit); SignalR backplane, MinIO still pending

Scheduled/Webhook Workflows       Multi-agent orchestration

Prometheus + Grafana              Additional channels

\`\`\`

**---**

**## Delivery Estimate**

\> Estimates for remaining work to reach each milestone under the pivoted plan.

\| Team Size | Pilot-Ready (Phases 0-7) | Scaled Enterprise |

\|---|---|---|

\| 1 Developer | \~21 weeks | \~30 weeks |

\| 3 Developers | \~8 weeks | \~12 weeks |

\| 5 Developers | \~5 weeks | \~8 weeks |

**---**

**## Pilot Success Criteria**

A department admin can:

1\. Connect an existing application (Swagger/URL)

2\. Have R2WAI auto-discover APIs, navigation, and capabilities

3\. Review and fix the auto-generated assistant configuration

4\. Test the assistant (chat with streaming, RAG citations)

5\. Execute a policy-governed tool call through the gateway (RBAC + ABAC enforced, audited)

6\. Run a workflow with human approvals

7\. Publish the assistant to the floating widget and an application channel

8\. Confirm a public visitor sees only public content

9\. Confirm an officer sees only jurisdiction-scoped data

10\. View monitoring and audit trails for all assistant/tool activity

If all 10 work end-to-end, **\*\*the R2WAI government pilot is successful.\*\***