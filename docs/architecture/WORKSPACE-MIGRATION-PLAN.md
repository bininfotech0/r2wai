# Architecture Migration Plan — Tenant → Workspace → Agent

**Date:** 2026-10-03
**Inputs:** `docs/audit/BACKEND-AUDIT-R2WAI-2.0-2026-10-03.md`, `docs/security/BACKEND-SECURITY-REVIEW-2026-10-03.md`
**Status:** PLAN ONLY. No schema, code, or configuration has been changed.

---

## 0. The decision that gates everything else

The 2.0 brief says *"DO NOT USE APPLICATION"*. In this repository **`Application` is three different things**, and only one of them is the product concept the brief wants to remove.

| # | Thing called "Application" | What it is | 2.0 disposition |
|---|---|---|---|
| 1 | The `R2WAI.Application` **project** | Clean Architecture application layer | **KEEP.** Renaming it would break every `using` and gain nothing. |
| 2 | `ConnectedApplication` (entity, table `Applications`) | A connector **definition**: name, base URL, environment, status, versions. Already carries `ApplicationApi` (auth + encrypted credential + OpenAPI source), `ApplicationConfiguration` (model, timeout, RAG threshold), `ApplicationVersion` (config snapshots) | **RENAMED → `Connection`.** This is the target concept. |
| 3 | `Department` → `Application` hierarchy | The organizational nesting the product no longer wants | **RETIRE, additively.** |

**Recommendation: rename (2) to `Connection`; do not rebuild it; keep the table name `Applications` for at least one release to avoid a risky rename of the most-referenced table in the schema.**

**This must be confirmed by the product owner before work starts.** Everything below is conditional on it.

---

## 1. Current-state dependency map

### 1.1 `Department` — the thing to retire

| Dependency | Location | Impact |
|---|---|---|
| Entity | `Domain/Entities/Department.cs:5` | — |
| `ConnectedApplication.DepartmentId` | `ConnectedApplication.cs:20` (**non-nullable**) | FK, indexed (`ConnectedApplicationConfiguration.cs`) |
| `ModelConfiguration.DepartmentId` | `ModelConfiguration.cs:10` | FK, indexed |
| `AccessRequest.Department` | `AccessRequest.cs:11` | free-text signup field, **not an FK** |
| Controller | `Api/Controllers/DepartmentsController.cs` | 5 actions; reads open to any user (`:34`,`:42`), writes `Admin,SystemAdmin` (`:18`,`:48`,`:57`) |
| Commands/Queries | `Features/Applications/Commands/{Create,Update,Delete}DepartmentCommand.cs`, `Queries/GetDepartment{,ById}Query.cs` | — |
| DTOs | `DepartmentDto.cs`, referenced by `ApplicationDto.cs`, `ModelConfigDto.cs` | — |
| Seeding | `ApplicationDbContextSeed.cs` (9 refs) | cross-tenant demo data |
| API surface | `GET/POST/PUT/DELETE /api/v1/Departments` | — |
| Client | `features/departments/` (4 files), `e2e/departments.spec.ts` (8 refs), `router.tsx` (2) | — |
| Migrations | 743 of the 966 total `Department` string occurrences in `src/` | historical DDL |

**Already in flight (uncommitted working tree — do not discard or duplicate):**
- `20261001082539_MakeConnectedApplicationDepartmentOptional` — `ConnectedApplication.DepartmentId` → nullable
- `20261001094300_AddDepartmentlessApplicationCodeUniqueness`
- `20261001192826_MakeAccessRequestDepartmentOptional`

This tells us the product has already decided departments become optional. Finish that; don't restart it.

### 1.2 `ConnectedApplication` → `Connection`

| Concern | Current home | Target |
|---|---|---|
| Connection identity (name, base URL, env, status) | `ConnectedApplication` | `Connection` |
| Auth scheme + encrypted credential + OpenAPI source | `ApplicationApi` | `ConnectionCredential` + `ConnectionOperation` |
| Discovered operations | `ToolDefinition` (one per op) | `ToolDefinition` (**keep as-is**) |
| Config snapshot / publish | `ApplicationVersion` | `ConnectionVersion` (optional) |
| Model + timeout + RAG threshold | `ApplicationConfiguration` | fold into `Connection` |

`DiscoverApplicationCommand.cs` is the discovery path to migrate (§1.5 below).

### 1.3 `AssistantDefinition` → `Agent`

Already 1:1 with the target. `AssistantDefinition` + `AssistantVersion` + `AssistantDefinition.PublishStatus` + `PublishedAssistantVersionId` + `Chatbot.PublishedAssistantVersionId` + `PublishConcurrencyToken`.

**Rename, do not create.** A parallel `Agent` entity would duplicate the definition, the version, the publish controller, the chat path, and the entire client feature folder.

### 1.4 What does not exist and must be built

| Target | Status | Notes |
|---|---|---|
| `Workspace` entity + `WorkspaceId` | **Zero occurrences in the backend** | Case-sensitive `WorkspaceId` search across `src`, `docs`, `tests`: 0 matches |
| Workspace membership / permission records | absent | |
| `AgentExecution` / `ToolExecution` / `KnowledgeExecution` (execution ledger) | absent | **Biggest missing capability.** Today there is no correlation between a chat, the tools it called, and the knowledge it retrieved. `Conversation`+`Message`, `WorkflowInstance`+`WorkflowStepExecution`, `AuditLog` are three unrelated silos. |
| `ConnectionCredential` as a unified entity | absent | 4 different storage mechanisms today (see audit §3.5) |
| Resource-level permission enforcement | absent | `User.HasPermission` is dead code with 1 occurrence in the whole repo |
| Chunk-level knowledge authorization | absent | Safe today only because a KB is single-tenant |

---

## 2. Target shape

```
Tenant  (highest security boundary — unchanged)
  └─ Workspace  (NEW — primary user-facing resource boundary)
       ├─ Agents            (rename of AssistantDefinition)
       │    └─ AgentVersions (rename of AssistantVersion)
       ├─ Connections       (rename of ConnectedApplication + ApplicationApi)
       ├─ Knowledge         (KnowledgeBase gains WorkspaceId)
       ├─ Automations       (Workflow gains WorkspaceId)
       ├─ Published Agents  (existing columns, workspace-scoped)
       ├─ Executions        (NEW ledger)
       ├─ Approvals         (gains WorkspaceId)
       └─ Audit             (gains WorkspaceId)
```

**Rules to enforce from day one:**
1. Every workspace-owned resource has a non-nullable `WorkspaceId` **after** backfill, and a `TenantId` **forever**.
2. Workspace is a **narrowing** of tenant access. It must never widen it.
3. `TenantId` is derived from the JWT claim only. `WorkspaceId` is derived from **server-side membership resolution**, never trusted from the client. If a client sends a workspace id, it is a *hint* that must be validated against the caller's membership set.
4. Background jobs carry **both** `TenantId` and `WorkspaceId` in the payload, and the processor validates payload-vs-row for both. (Today `BackgroundJob` has neither — see security H-8.)

---

## 3. Phased plan

Each phase is independently shippable and independently revertible. **No phase drops a table.**

### Phase 0 — Fix what is already broken (prerequisite, not optional)

Independent of the domain change. Fix in the order given in `docs/security/BACKEND-SECURITY-REVIEW-2026-10-03.md`:

| Step | Item | Why it blocks |
|---|---|---|
| 0.1 | SSRF: DNS resolution + redirect constraints (C-1, C-2) | Adding more connectors multiplies the reachable surface |
| 0.2 | `ApprovalPolicy` CRUD authorization (C-3) | Workspace membership makes self-grant *easier*, not harder |
| 0.3 | Plaintext webhook secret out of the audit log (H-2) | Audit rows will be filtered by `WorkspaceId` soon — copy forward the leak |
| 0.4 | Make security tests fail loudly (L-5) | Every later phase's tests depend on trustworthy tests |
| 0.5 | Enforce or delete `ConfirmationRequired` (H-5) and `MinApprovers` (H-6) | Workspace-scoped governance that is write-only is worse than none |

**Exit criteria:** new tests for each; full suite passes; no test can silently no-op.

### Phase 1 — Additive Workspace schema (no behaviour change)

1. New `Workspaces` table: `Id`, `TenantId`, `Name`, `Slug`, `Description`, `Status`, `IsDefault`, `CreatedBy`, audit columns. Unique index on `(TenantId, Slug)`.
2. New `WorkspaceMembers` table: `WorkspaceId`, `UserId`, `Role`, `CreatedAt`. Unique on `(WorkspaceId, UserId)`.
3. **Nullable** `WorkspaceId` on: `AssistantDefinition`, `ConnectedApplication`, `ApplicationApi`, `McpServerConnection`, `KnowledgeBase`, `Workflow`, `ApprovalRequest`, `AuditLog`.
4. Composite indexes `(TenantId, WorkspaceId)` on each; `WorkspaceId` FK with `Restrict` delete behaviour (never cascade inside a tenant boundary).
5. EF migration. **Additive only.** Application code unchanged; the property is unused until Phase 3.

**Gates:** migration applies on a production-like restored copy; `dotnet ef migrations script` output reviewed; row counts unchanged; rollback is a plain `Down()`.

### Phase 2 — Backfill (idempotent, measured, reversible)

1. Create one **default workspace** per existing tenant, named after the tenant.
2. Assign every existing workspace-owned resource to its tenant's default workspace.
3. Map existing `Department` → workspace **only if** the business confirms it. **Default assumption: do not map departments to workspaces.** Departments and workspaces are different axes; auto-mapping them would silently relocate every application.
4. Run as an idempotent migration or a resumable job. Emit a report: rows scanned, rows mapped per table, orphans, ambiguities. **Stop on any unmapped row.**
5. Add `NOT NULL` on `WorkspaceId` in a **separate** later migration, only after the report is clean for a full business cycle.

**Gates:** counts match exactly; FK integrity check passes; dry-run report reviewed by a human; backup taken and **restore tested** before running against real data.

**Rollback:** drop the new tables and the nullable columns. No existing data is modified in Phase 2, so rollback is total.

### Phase 3 — Server-side workspace resolution + enforcement

1. `IWorkspaceContext` resolved per request from the authenticated user's memberships. Populate from the token/DB, never from a request body or query string.
2. `WorkspaceScopeBehavior` (MediatR pipeline, alongside the existing `AuthorizationBehavior`) — any request implementing `IWorkspaceScopedRequest` must carry a workspace in scope.
3. **Workspace global query filter**, mirroring the existing tenant filter style (`ApplicationDbContext.cs:306-330`) and applied in the same reflective loop, with the same fail-closed semantics: *no workspace context → no rows*, exactly as *no tenant → no rows* today.
4. Extend `BackgroundJob` with `TenantId` **and** `WorkspaceId`; the processor validates both against the row (security H-8).
5. Extend `PgVectorService` with a `tenant_id` column and thread tenant through `IVectorStoreService` (security H-7).

**Gates:** new integration tests — cross-tenant read/write, cross-workspace read/write, guessed ids, no membership, background execution, vector retrieval, tool execution, publish, audit visibility. Each must assert **denial**, not absence of error.

**Rollback:** disable the workspace filter via configuration; the nullable columns remain, data intact.

### Phase 4 — Rename `AssistantDefinition` → `Agent` (no table rename)

Pure code rename of class, DTO, feature folder, and route. **Keep table name `AssistantDefinitions` and all existing routes** (`/api/v1/Assistants`) with the new names as aliases.

**Gates:** existing assistant tests pass unchanged; new agent-route tests pass; client continues to work against the old routes.

**Rollback:** remove the aliases.

### Phase 5 — Rename `ConnectedApplication` → `Connection` (no table rename)

1. Code rename; add `/api/v1/Connections` as an **alias** of `/api/v1/Applications`.
2. Migrate discovery: `DiscoverApplicationCommand` → `DiscoverConnectionCommand`. **Preserve behaviour exactly:**
   - `openApiImportService.AnalyzeAsync` (the `Microsoft.OpenApi.Readers` parser) is reused unchanged.
   - One `ToolDefinition` per discovered operation, still created **inactive** (`DiscoverApplicationCommand.cs:88-92` — `capability.Deactivate()`), still governed by `DefaultGovernanceForHttpMethod`.
   - Discovery **must not** auto-activate. This is the requirement in §17 of the brief and the current code already satisfies it.
3. Add a `ConnectionOperation` projection so a connection's operations are listable independently of tool activation.

**Gates:** `DiscoverApplicationCommandHandlerTests` and `OpenApiImportServiceTests` pass unchanged; new tests assert discovered operations land inactive.

**Rollback:** remove the alias routes; nothing in the schema changed.

### Phase 6 — Execution ledger (the real missing capability)

New `Executions` + `AgentExecution` / `ToolExecution` / `KnowledgeExecution` tables carrying `TenantId`, `WorkspaceId`, `AgentId`, `AgentVersionId`, `UserId`, `Status`, `StartedAt`, `CompletedAt`, `DurationMs`, `CorrelationId` — and **no prompt or response content unless a retention policy explicitly allows it**.

Wire `CorrelationId` (already generated by middleware) through the streaming path and `ToolGateway` audit writes so a chat, its tool calls, and its knowledge retrievals become one traceable unit.

**This is a build, not a rename.** Budget it as its own release.

### Phase 7 — Workspace-scoped API surface

Add `/api/v1/workspaces/{workspaceId}/…` routes for agents, connections, knowledge, automations, publish, activity, approvals. Keep all existing tenant-scoped routes working for the deprecation window; reject tenant/workspace mismatches with 403.

### Phase 8 — Frontend migration

Workspace selector sends a hint; backend authorizes it. Migrate screens in order: Workspaces → Connections → Agents → Knowledge → Automations → Publish → Activity. **Keep the legacy Departments and Connected Applications screens reachable until Phase 10 evidence exists.**

### Phase 9 — Deprecate

Mark `/api/v1/Departments` and department fields obsolete in OpenAPI. Log usage. Publish deprecation dates. Measure real traffic for at least one full business cycle.

### Phase 10 — Retire (separate, backup-verified release)

Only with: zero measured traffic on deprecated endpoints · all clients migrated · data reconciled · a **tested** backup and restore · a rehearsed rollback.

**Delete nothing blind.** Historical `Department` / `Application` rows referenced by `AuditLog.ApplicationId` must be preserved for audit integrity, or the FK relaxed (`AuditLogConfiguration.cs:59-62` is `SetNull`, so an `Application` deletion nulls the audit link — decide deliberately whether that is acceptable).

---

## 4. Authorization migration

Current state: 3 roles (`Admin`, `SystemAdmin`, `User`), 5 hard-coded policies (`Program.cs:146-166`), and a **dead** permission model (`User.HasPermission` — 1 occurrence repo-wide).

| Target permission | Nearest current control | Gap |
|---|---|---|
| `Agent.Read` / `Create` / `Update` / `Test` / `Publish` | `AssistantsController` — `[Authorize]` only | No per-action control; any tenant member can publish |
| `Connection.*` | `ApplicationsController` — `[Authorize]` only | Same |
| `Knowledge.*` | `KnowledgeBasesController` `[Authorize]`; `DocumentsController` `CanManageDocuments` | Inconsistent |
| `Automation.*` | `WorkflowsController` `CanManageWorkflows` | Coarse |
| `Approval.Decide` | `ApprovalService.VerifyApproverAuthorization` + policy roles | Sound, but policy CRUD is open (security C-3) |
| `Tool.Activate` | `ToolGateway` + `RequiredRole` on `ToolDefinition` | Per-tool, works |

**Sequence:** first close C-3; then decide whether to implement the permission evaluator or remove the permissions UI. **Do not ship an unenforced permissions field** — it is the current state and it is a liability. Workspace membership then becomes a *third* axis alongside role and permission, resolved server-side in `IWorkspaceContext`.

---

## 5. Test migration

| Layer | Current | Required for Workspace |
|---|---|---|
| Domain | 200 tests, entity behaviour | `Workspace` invariants; `WorkspaceMember` uniqueness |
| Application | 124 tests, handler logic with Moq | workspace-scoped command/query handlers |
| API integration | 168 controller tests | cross-tenant **and** cross-workspace denial tests |
| Security | 187 tests | membership enforcement; client-supplied `WorkspaceId` cannot widen access |
| Postgres/Testcontainers | 114 tests incl. pgvector | backfill idempotency; vector-store tenant column; job tenant+workspace columns |
| Infrastructure | 343 tests | `WorkspaceContext` resolution; policy evaluators |

**Mandatory: eliminate soft-skips** (audit §6). `RoleMatrixSecurityTests` alone has 12 tests that `return;` without asserting when login fails — the only negative role-matrix coverage in the repository.

---

## 6. Rollback strategy

| Phase | Rollback |
|---|---|
| 0 | Revert commits. No schema change. |
| 1 | `Down()` — drop added tables/columns. Nothing else touched. |
| 2 | `Down()` — no existing row was modified. |
| 3 | Disable the workspace filter by configuration. Columns remain. |
| 4–5 | Remove aliases; old routes/classes restored by revert. |
| 6 | Feature flag the ledger writes off; tables remain. |
| 7 | Remove new routes; legacy routes unaffected. |
| 8–9 | Frontend flag. |
| 10 | **Requires a tested restore.** No rollback by deletion. |

**Universal rule: never roll back by deleting newly mapped data.**

---

## 7. Blockers requiring a decision or environment access

| # | Blocker | Owner |
|---|---|---|
| 1 | Confirm `ConnectedApplication` → `Connection` (not a rebuild) | Product |
| 2 | Do Departments map to Workspaces, or are they orthogonal axes? **Default: orthogonal.** | Product |
| 3 | One default workspace per tenant, or per-department? | Product |
| 4 | Live migration level and per-tenant Department/Application row counts | DBA / environment |
| 5 | Which external clients depend on `/api/v1/Applications`, `/api/v1/Departments`, `/api/v1/Assistants`? | Engineering |
| 6 | Should `ApplicationConfiguration.RagThreshold` live on `Connection` or on the KB? | Architecture |
| 7 | Is `AssistantDefinition` → `Agent` a pure rename acceptable to the client team? | Frontend |
| 8 | Does the RAG index need per-document ACLs before Knowledge is shared across workspaces? (security M-7) | Security |
| 9 | Retain or null out `AuditLog.ApplicationId` on connection deletion? | Compliance |

---

## 8. What this plan explicitly does **not** do

- Does not delete `Departments` or `Applications` tables, columns, or rows.
- Does not rename any database table.
- Does not remove Elsa, the background job queue, or any working infrastructure.
- Does not introduce a second workflow engine, policy framework, or vector store.
- Does not touch `Department`/`Application` **rows** — only their optionality, which is already in flight.
- Does not claim any of Phases 0–10 have been implemented.

**None of this work has started.** The repository is unchanged apart from the two new documentation files.