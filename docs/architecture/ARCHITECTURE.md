# Architecture

> Recreates the coverage the old root `ARCHITECTURE.md` had (confirmed deleted from the tree —
> `docs/api/MISSING-BACKEND-ENDPOINTS.md` already points here instead). Current-state facts are
> from direct repository verification; the target architecture is ported from
> `docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md` §54, cross-referenced against `ROADMAP.md` §3. See
> also `docs/architecture/AI-RUNTIME.md`, `TOOL-GATEWAY.md`, `DATA-MODEL.md`,
> `EXECUTION-AND-WORKFLOWS.md` for the pieces broken out separately.

## Current state

One deployable modular monolith, Clean Architecture, four backend projects:

- **`R2WAI.Domain`** — entities, enums, no framework dependencies.
- **`R2WAI.Application`** — MediatR commands/queries, validators, interfaces (`ICurrentUserService`,
  `IAgentRuntime`, `IWorkflowBridge`, `IPromptTemplateService`, etc.) — the seams everything else
  plugs into.
- **`R2WAI.Infrastructure`** — EF Core/Postgres, Semantic Kernel, Elsa activities, Redis, MinIO,
  encryption, background jobs — implements the Application-layer interfaces.
- **`R2WAI.Api`** — controllers, SignalR hubs, middleware, `Program.cs` composition root.

Plus `R2WAI.Client` (React 19/TS/Vite/MUI SPA) and `R2WAI.Widget` (standalone embeddable bundle,
zero runtime deps).

**Known deviation:** a handful of controllers (`ApprovalsController`, `ApiKeysController`,
`AuthController`, `AdminController`, `WorkflowsController`) inject `ApplicationDbContext` directly
instead of routing through MediatR — meaning MediatR's `AuthorizationBehavior`/`ValidationBehavior`
protect only handlers that go through it; these five rely on per-action attributes instead. Not
fixed as part of the current implementation plan — no concrete defect traced to it, unlike the
items the plan does address.

## Target architecture

Keep one deployable modular monolith, run in two roles from the same image (`API` and `Worker`) so
sweepers/executors scale independently of request traffic:

```
                       ┌────────────── Control plane ──────────────┐
 Studio (React) ──API──▶ Tenants · Departments · Applications        │
 Widget ────────public─▶ Capabilities(versioned) · Policies · Models │
                       │ Secrets(SecretRef) · Approvals config       │
                       └───────────────┬─────────────────────────────┘
                                       │ (PostgreSQL = truth)
   ┌────────────── AI plane ───────────┴──────────── Execution plane ─────────────┐
   │ Assistant runtime (SK / Agent Framework adapter)  Run engine (leases, attempts) │
   │ ModelRouter · RAG pipeline                        ApprovalGate (park/resume)    │
   │   │ ActionProposal (structured)                   Outbox dispatcher             │
   │   └────────────────▶  IToolGateway  ◀─────────────  Workflow "capability" step │
   │      identity·tenant·permission(RBAC+ABAC)·policy·risk·approval·idempotency   │
   │      ·egress guard·secret resolve·execute·verify·ExecutionRecord·Audit        │
   └───────────────────────────────┬───────────────────────────────────────────────┘
                                   ▼ REST / MCP / SMTP connectors
                            Enterprise applications
   Cross-cutting: OpenTelemetry spans per stage · append-only AuditEvent · Redis (cache only)
```

**Rule:** the model only emits *proposals*; deterministic code executes. Every path — assistant
tool call, workflow step, future MCP or Agent Framework call — crosses the *same* `IToolGateway`.
Elsa (kept) is a step executor inside the run engine, not a second source of truth.

## Path from current to target

See the phased implementation plan (`C:\Users\LENOVO\.claude\plans\quiet-jumping-mist.md` as of
2026-09-30, or wherever it's since been committed to the repo) for the concrete sequencing:
Publish module → Tool Gateway extraction → MCP → Agent Framework (single-turn) → durable
execution ledger → navigation consolidation. Each phase is independently shippable; this document
should be updated as each phase lands rather than left to drift.
