# R2WAI transformation — Phase 0 baseline and Phase 1 inventory

2026-09-20 · branch `feature/react-studio-migration` · the working tree still holds the 44 uncommitted files from the first remediation pass · **no code, config, migration or package was changed to produce this document.**

Companion to [R2WAI-IQ200-AUDIT-2026-09-20.md](R2WAI-IQ200-AUDIT-2026-09-20.md). That report remains the record of *findings*. This one records the *baseline*, the *inventory needed to remove Elsa and Semantic Kernel*, and re-issues the KEEP/REMOVE decisions under the new direction.

Evidence tags: **[X]** executed today · **[R]** read in source today · **[G]** established by search today (an absence claim means "not found by search") · **[A]** taken from the audit, not re-verified today · **UNVERIFIED**.

---

## 1. The direction changed, and what that does to the open P0s

The audit's §63 said *"Do not replace Elsa, Semantic Kernel or PostgreSQL."* The transformation brief now says the opposite for two of them: **remove Semantic Kernel and Elsa, add Microsoft Agent Framework behind R2WAI-owned abstractions, and stop being a workflow product.** The brief is authoritative, so §63 is reversed. Consequences for the items still open in the audit's remediation log:

| Audit item | Effect of the new direction |
|---|---|
| P0-1 reject continues the run (fixed in `WorkflowBridge`) | The fix disappears with `WorkflowBridge`. Its *intent* must be ported: a declined confirmation cancels the execution and nothing after it runs. Keep the assertions in `WorkflowBridgeApprovalTests` as the acceptance test for the new Confirmation → Execution transition. |
| P0-3 four sweepers have no lease | Two of them (`WorkflowScheduleBackgroundService`, `WorkflowDelayResumeBackgroundService`) are deleted with Elsa. **Do not build leases for them.** Left: Escalation and DataRetention. Keep the 1-replica pin until those two are lease-based. |
| P0-9 workflow "API Call" step bypasses the gateway | Half of it disappears: `ApiCallNodeProvider` is deleted with Elsa. The other half, plaintext credentials in `ToolDefinition.Configuration`, stays and becomes `ISecretProvider` (brief Part 45). |
| P0-8 SSRF | Becomes one `EgressGuard` inside the Tool Gateway (brief Parts 16, 18), plus the knowledge-base URL fetcher. Not to be added call site by call site. |
| P0-5 tenant filter fail-open, 9 unfiltered entities, vector tenant column | Unchanged and independent of the removals. Its own track. |
| P0-2 idempotency / `ToolExecution` | Unchanged in substance; lands as `IdempotencyRecord` + `ToolExecution` in the durable ledger (brief Part 20). See §3.6 — today's only store is in-memory. |

---

## 2. Phase 0 — baseline (executed today)

| Check | Result |
|---|---|
| `dotnet build R2WAI.slnx` | **0 errors, 22 warnings** [X]. This was an *incremental* build (3 s, binaries were current), not a clean rebuild. The test build also surfaced `NU1903`: `System.Security.Cryptography.Xml 10.0.7` has a known high-severity advisory (GHSA-mmjf-rqrv-855v) [X]. |
| `R2WAI.Domain.Tests` | 185 / 185 [X] |
| `R2WAI.Application.Tests` | 59 / 59 [X] |
| `R2WAI.Infrastructure.Tests` | 212 / 212 [X] |
| `R2WAI.Api.Tests`, namespaces `Security`, `Middleware`, `Services` (where the remediation-pass tests live) | 219 / 219, 2 m 41 s [X] |
| Vitest | 56 / 56, 12 files [X] |
| `tsc -p tsconfig.app.json --noEmit` | clean [X] |
| **Not run** | The rest of `R2WAI.Api.Tests` (only the subset above ran), Playwright e2e, Docker image build, `oxlint`, widget build, and EF migrations against a live PostgreSQL (the migrations were run against PostgreSQL 16 during step B, below). **This baseline does not show the app runs end to end.** |
| Environment | .NET SDK 10.0.204, `feature/react-studio-migration`, 44 modified/untracked files, nothing committed [X] |

The Codebase Memory MCP tools named in `.claude/CLAUDE.md` were not available in this session (no matching tool could be loaded), so everything here is from Grep/Glob/Read.

---

## 3. Phase 1 — inventory

### 3.1 Semantic Kernel: where it actually is

Packages [R]: `Microsoft.SemanticKernel 1.77.0` and `Microsoft.SemanticKernel.Plugins.Core 1.77.0-preview`, in `R2WAI.Infrastructure.csproj` only. Domain and Application have no SK package, so their three hits (`IEnabledToolScope`, `CreateModelCommand`, `ToolType`) are text, not types [G].

| Where | Role | SK-typed surface | Replacement |
|---|---|---|---|
| `Infrastructure/AI/SemanticKernelService.cs` (595 lines) | implements `IAIService`: chat, stream, generate, summarise, extract, compare, embeddings; kernel cache; attaches tools | `Kernel`, `IKernelBuilder`, chat and embedding connectors | `IChatClient` / `IEmbeddingGenerator` (Microsoft.Extensions.AI) behind the *same* `IAIService`; tool turns move to an R2WAI agent |
| `AI/ModelGateway/*` (`IModelProvider`, OpenAI/Ollama/Z.ai) | provider strategy | **the interface itself is SK-typed**: `Configure(IKernelBuilder, …)` | `IR2WAIModelGateway` returning `IChatClient`; providers stop taking a kernel builder |
| `AI/AiFunctionAuditFilter.cs` (326) | governance: registry lookup, role, risk ceiling, approval-above-risk, audit | `IFunctionInvocationFilter` — governance is a hook on someone else's pipeline | moves *into* the tool: every `IR2WAITool` executes through `IToolGateway`, so there is nothing to hook or forget to attach |
| `AI/GovernedKernelBuilder.cs` (38), `BuiltInToolGovernance.cs` | fail-closed attach, code-default governance | `Kernel.Clone()`, filters | disappears; fail-closed becomes "no gateway, no tool" |
| `AI/DynamicTools/DynamicToolFunctionFactory.cs` (116), `DynamicToolExecutor.cs` (170) | turns `ToolDefinition` rows into callable functions | `KernelFunctionFactory.CreateFromMethod` with **one free-text `string? input`** parameter [R] | `IR2WAITool` carrying a real JSON `InputSchema`. This is also what makes parameterised reads possible (audit Appendix D). |
| `AI/Plugins/{Workflow,Document,RAG,Assistant}Plugin.cs` (232 / 95 / 85 / 71) | built-in tools | `[KernelFunction]` methods | `IR2WAITool`s; `WorkflowPlugin` is deleted with Elsa |
| `SemanticKernelService.cs:590-591` | adds `ConversationSummaryPlugin` and `TimePlugin` to every kernel [R] | the only users of the **preview** `Plugins.Core` package [G] | `TimePlugin` is a few lines as an R2WAI tool; summarising already exists as `IAIService.SummarizeTextAsync`. The package can go. |
| `Api/Controllers/AdminController.cs:312` | model "test connection" builds `Kernel.CreateBuilder()` inside a controller | | route through the model gateway |
| `Api/Workflows/InvokeSemanticKernelActivity.cs`, `NodeProviders/AiGenerateNodeProvider.cs` | Elsa activity that calls AI | | deleted with Elsa |

**Why this is tractable:** every caller goes through `IAIService` — 22 files: 7 Application handlers, 4 controllers, 6 Infrastructure services, `AgentRuntime`, `ConversationMemoryService`, and 2 Api workflow classes [G]. SK can be swapped *behind the existing interface* with no caller change, and callers migrate to R2WAI abstractions afterwards. SK-typed code outside `Infrastructure/AI` is exactly `AdminController`, the Elsa activity and provider, and one line in `Program.cs` [G].

**Tests that pin today's behaviour** and must be re-pointed, not deleted: `ToolGovernanceFilterTests` (drives real SK), `GovernedKernelBuilderTests`, `BuiltInToolGovernanceTests`, `ModelGatewayTests`, `AiFunctionAuditFilterTests`, `ChatConcurrencyRegressionTests` [G].

### 3.2 Elsa: where it actually is

Packages [R]: five, all in `R2WAI.Api.csproj` — `Elsa`, `Elsa.Email`, `Elsa.Http`, `Elsa.Persistence.EFCore.PostgreSql`, `Elsa.Scheduling`, all 3.7.0. Registered at `Program.cs:59-99` (only when an Elsa connection string exists and the environment is not `Testing`); its persistence migrations run at startup (`Program.cs:~495-505`) [R].

**Domain leakage is two fields:** `WorkflowInstance.ElsaInstanceId` (+ `SetElsaInstanceId`) and `ApprovalRequest.ElsaBookmarkId` (+ `SetElsaBookmarkId`) [G]. `WorkflowInstance`'s own comment says Elsa's native Delay + bookmark resume "doesn't work reliably here", so a Delay step never runs as a real Elsa activity [R] — consistent with the audit's finding that R2WAI re-starts a fresh Elsa run per continuation.

| Piece | Files | Elsa-specific? | Fate |
|---|---|---|---|
| Elsa runtime, persistence, Http/Scheduling/Email/JavaScript modules | `Program.cs`, 5 packages | yes | delete |
| Custom activities | `ApprovalStepActivity`, `TransformStepActivity`, `InvokeSemanticKernelActivity`, `StepActivityFactory` | yes | delete |
| Node providers | `Api/Workflows/NodeProviders/*` (9 providers + `INodeProvider`) | yes | delete (`ApiCallNodeProvider` was the ungoverned HTTP path) |
| Elsa notification handlers | `StepStatusNotificationHandler`, `WorkflowInstanceCompletionNotificationHandler` | yes | delete; `StatusHub` streaming is generic and stays, fed by the execution ledger instead |
| Bridge | `WorkflowBridge` (481), `NoOpWorkflowBridge`, `IWorkflowBridge` | yes — its job is to drive Elsa | delete |
| Feature surface | `WorkflowsController` (554), `WorkflowService`, `IWorkflowService`, `WorkflowPlugin` | mostly | delete with Automations |
| Sweepers | `WorkflowScheduleBackgroundService`, `WorkflowDelayResumeBackgroundService` | yes | delete |
| `WorkflowInstance` + `WorkflowStepExecution` | Domain | **generic shape** (status, timing, step rows) | reshape into `Execution` / `ExecutionStep`; drop `ElsaInstanceId` |
| `ApprovalRequest`, `ApprovalService`, `ApprovalPolicy`, `EscalationBackgroundService`, `ApprovalsController` | | generic, except `ElsaBookmarkId` and the hard FKs to `WorkflowInstanceId` / `WorkflowId` [A] | reshape into `Confirmation` bound to an execution and an action hash; keep escalation; add expiry |
| `StatusHub`, notifications | | generic | keep |

**Database.** R2WAI's own 44 migrations are R2WAI's and stay (historical reproducibility). Elsa's tables come from migrations compiled *inside the Elsa package* (see the comment at `Program.cs:63-66`), so there is no R2WAI migration file to delete for them; existing dev databases will keep orphaned Elsa tables. Their schema and table names were not read → **UNVERIFIED**, needs a live database. Nothing is in production, so a one-off drop is acceptable.

Test and e2e footprint [G]: `WorkflowBridgeApprovalTests`, `ApprovalRequestTests`, `WorkflowInstanceTests`, `e2e/approvals.spec.ts`.

**Removing Elsa first also shrinks SK removal:** it deletes `InvokeSemanticKernelActivity`, `AiGenerateNodeProvider` and `WorkflowPlugin`, three of the SK-typed files above.

### 3.3 Workflow UI

Routes [R, `router.tsx`]: `/automations`, `/automations/:id`, `/automations/:id/builder`. `features/automations` holds the list, detail, the `@xyflow` builder, node palette, step schemas, editor drawer, status-hub hook and create dialog. It is also woven into `features/playground` (`AutomationPlaygroundPane`, `ExecutionInspector`), `features/runs` (`RunsPage`, `RunInspectorDrawer`), `features/approvals`, the dashboard `HomePage` (22 mentions), `components/CapabilityFlowDiagram`, `CreateEditWebhookDialog`, `UniversalCreate` and `CommandPalette`. "workflow" or "automation" appears 485 times in 60 client files [G]. "Automations" is in the Build nav for Admin and SuperAdmin and in the end-user menu (`roleNav.ts:41,95`) [R].

### 3.4 Legacy and dead code (brief Part 52)

| Item | Evidence | Production path? | Tested? | Class → Action |
|---|---|---|---|---|
| **Member wallet / points / withdrawals / plan upgrade / event points** | `MembersController` serves `api/v1/members` and `api/v1/admin/members` (wallet, request/approve/reject/complete withdrawal); 18 Domain/Application files (`MemberWallet`, `WithdrawalRequest`, `PointsTransactionReason`, `AwardEventPointsCommand`, `ConvertPointsCommand`, `ApprovePlanUpgradeCommand`, …) [G]; migrations `AddMemberRewards`, `AddMemberAadhaarFields` | **live API, no UI** — the React client has no reference [G] | no test references it [G] | LEGACY → REMOVE (forward migration drops the tables; keep historical migrations). `register-member` / Aadhaar login is entangled (`RegisterMemberCommand`, `User.cs`) and carries the audit's unsalted-hash finding. |
| `NoOpWorkflowBridge` | used in `Testing` only [G] | no | n/a | DEAD_CODE once Elsa goes → REMOVE |
| `Microsoft.SemanticKernel.Plugins.Core` (preview) | two tools only (§3.1) | yes, trivially | via `BuiltInToolGovernanceTests` | CONFIGURED_NOT_USED beyond two tools → REMOVE |
| Blazor / MudBlazor | **no `.cs` or `.csproj` reference**; 7 mentions in `ci.yml`, `docker-compose.yml`, `studio.conf`, and 24 in four `.md` files [G] | no | n/a | code is clean; docs are stale → MODIFY docs |

### 3.5 What already exists that the target names (so MODIFY, not build)

- `IAgentRuntime` / `AgentRuntime` (23 lines): a facade that always enables tools [R] → the seam for `IR2WAIAgent`.
- `IModelGateway`, `IModelProvider`, `ModelConfigurationResolver`: exist but SK-typed [R].
- `ToolDefinition` already carries governance fields; `ApplicationApi`, `IOpenApiImportService` and `DiscoverApplicationCommand` exist (`Microsoft.OpenApi.Readers 1.6.22`) [G]. Migrations `AddBusinessCapabilities` and `AddCapabilityGovernanceToToolDefinitions` exist [G]. A capability concept exists, stored on `ToolDefinition`, with no registry or resolver.
- `NavigationDefinition`: CRUD only; nothing in AI/chat consumes it [A].
- `lib/chat/responseCards` exists in the client; whether its contract matches brief Part 21 was not audited → **UNVERIFIED**.

### 3.6 Idempotency today

`IIdempotencyStore` (`Get/Set/Exists`) has one implementation, `InMemoryIdempotencyStore`: a `ConcurrentDictionary` with a 24 h TTL [R]. It is per-process (lost on restart, not shared across replicas) and get-then-set is not an atomic claim, so it cannot satisfy the brief's crash scenario (Part 20: API succeeds, R2WAI crashes, user retries). The durable version needs an atomic claim in PostgreSQL (`INSERT … ON CONFLICT DO NOTHING`) keyed by tenant + capability + idempotency key. PLACEHOLDER → REPLACE.

---

## 4. Decision matrix under the new direction (brief Part 66)

| Component | Evidence | Decision | Reason | Migration |
|---|---|---|---|---|
| .NET 10, ASP.NET Core | SDK 10.0.204 [X] | KEEP | | |
| Clean Architecture | 4 src projects; controllers bypass Application in places [A] | KEEP | right shape for a modular monolith | route controllers through handlers as they are touched |
| React 19, TS, Vite, MUI, TanStack Query | `tsc` clean, Vitest 56/56 [X] | KEEP | | |
| PostgreSQL, EF Core, pgvector | [A] | KEEP | | add tenant column to the vector table [A] |
| Redis | compose and k8s [A] | KEEP | | |
| MinIO | client exists, no manifest deploys it, default storage is local disk [A] | KEEP, finish | per-pod local disk breaks with replicas | manifest + `Storage__Provider` |
| SignalR, SSE | 3 hubs [A] | KEEP | | authorisation fixes already in the working tree |
| OpenTelemetry, Prometheus, Grafana, Jaeger | containers only, no dashboards [A] | KEEP, MODIFY | | provision dashboards; correlation ids per brief Part 53 |
| Docker, Kubernetes | manifests exist [A] | KEEP, MODIFY | YAML is not production readiness | securityContext, NetworkPolicy, PDB [A] |
| MediatR 12.4.1, FluentValidation 11 | csproj [R] | KEEP | | verify the licence of any newer MediatR major before upgrading |
| AutoMapper 16.1.1 | csproj [R]; no licence key configured [A] | MODIFY | licence to verify | replace with hand mapping if commercial |
| **Semantic Kernel 1.77** | 37 files, Infrastructure only [R][G] | **REMOVE** | brief Part 7 | strangler behind `IAIService` (§5) |
| SK `Plugins.Core` preview | 2 tools [G] | REMOVE | | trivial R2WAI tool |
| **Elsa 3.7.0** | 5 packages, `Program.cs` [R] | **REMOVE** | brief Part 8; already bypassed by the code [A] | §5 |
| Workflow / Automations UI | 485 mentions, 60 files [G] | REMOVE (isolate as legacy first) | brief Part 25 | §5 step A |
| **Microsoft Agent Framework** | not referenced anywhere [G]; NuGet has `Microsoft.Agents.AI` 1.22.0, `.Abstractions` 1.22.0, `.OpenAI` 1.22.0 [X] | **ADD** (Phase 5) | adapter only; **not** `Microsoft.Agents.AI.Workflows`, which is a workflow engine | `R2WAI.Infrastructure.AI.AgentFramework` |
| Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`) | not referenced directly today [G] | ADD (Phase 4) | the common base under Agent Framework; lets the SK swap land before AF | |
| MCP | none [A] | ADD (Phase 10) | | gateway + server/tool allowlist |
| OpenAPI discovery | importer + `DiscoverApplicationCommand` exist [G] | MODIFY | PARTIAL: no review/publish step, schemas not carried into an `InputSchema` | admin review then publish |
| RAG (`KnowledgeBaseService`, `PgVectorService`) | [A] | KEEP, MODIFY | vector table has no tenant column [A]; versioning, effective/expiry dates, injection handling missing per brief Part 2A | |
| Widget | origin allowlist exists; no per-embed identity or rate limit [A] | MODIFY | brief Part 39 | per-embed token |
| Member wallet / withdrawals | 18 files, live API, no UI [G] | REMOVE | legacy | forward migration |
| `IIdempotencyStore` (in-memory) | §3.6 [R] | REPLACE | not durable | PostgreSQL `IdempotencyRecord` with atomic claim |

---

## 5. Recommended sequence

Elsa first (it is what the brief lists first, and it shrinks SK removal). Each step is test-first and leaves the build green. Independent tracks (P0-5 tenant filter, `ISecretProvider`, egress guard in the gateway) do not wait for any of these.

| Step | Brief phase | Change | Gate |
|---|---|---|---|
| **A. Isolate — done** | 2 / 3 | Remove Automations from every nav and mark the route legacy; rename Runs → Executions and Approvals → Confirmations in nav and page titles. No backend change. Reversible. | `roleNav.test.ts` updated first; `tsc`; Vitest |
| **B. Detach approvals — done** | 3 | `ApprovalRequest` no longer needs a workflow (nullable links, `Subject`, dead `ElsaBookmarkId` dropped); forward migration. *Corrected from the first draft — see the step B result below.* | Domain, Api approval tests; migration applied to real PostgreSQL |
| **C. Delete Elsa** | 3 | Five packages, `Program.cs` blocks, `Api/Workflows`, bridge, the two sweepers, `WorkflowsController`, `WorkflowPlugin`, Automations UI, `WorkflowInstance.ElsaInstanceId` (moved here from B: the bridge and its tests run on it), a one-off Elsa-table drop | build; all suites; `AnonymousSurfaceTests` updated for removed routes |
| **D. Gateway core** | 4 | `IR2WAITool` (with `InputSchema`) and `IToolGateway` in Application; move `AiFunctionAuditFilter` logic into the gateway; SK becomes a thin adapter over R2WAI tools | `ToolGovernanceFilterTests` (real SK) pass **unchanged** |
| **E. Swap SK** | 4 | `IChatClient`/`IEmbeddingGenerator` model gateway behind `IAIService`; delete SK and `Plugins.Core`; `AdminController` model-test via the gateway | `ModelGatewayTests` re-pointed; `ChatConcurrencyRegressionTests` |
| **F. Agent Framework** | 5 | `R2WAI.Infrastructure.AI.AgentFramework` implementing `IR2WAIAgent` over the same tools | agent-turn tests against a fake model |

**Step A result (done 2026-09-20, uncommitted).** Automations moved out of Build and the end-user menu into a trailing admin-only "Legacy" nav section; the mobile bottom nav no longer links to it (`getBottomNavItems` moved into `roleNav.ts` so it is testable); the list and detail pages carry a `LegacyNotice`, the builder does not (its fixed `calc(100vh - 120px)` layout has no room for a banner, and it is only reachable from the detail page). Runs → Executions and Approvals → Confirmations in nav, breadcrumbs, page titles, KPI labels, About and Settings; **route paths are unchanged**. Verified: Vitest 63/63 (7 new), `tsc` clean. Playwright specs (`runs`, `approvals`, `monitor`, `unsaved-changes`) were updated to match but **not run**. Still linking into Automations from primary screens, deliberately left for the dashboard step and step C: dashboard `HomePage` (KPI tile, recent-activity rows, recent-automations table) and the `ApplicationWorkspacePage` automation chips.

**Step B result (done 2026-09-20, uncommitted), and a correction to this plan.** The first draft of step B ("reshape `WorkflowInstance` into `Execution`, drop both Elsa columns, port the P0-1 test") could not be done on its own. `WorkflowInstance.ElsaInstanceId` is the key `WorkflowBridge` and its tests run on, so it moved to step C. There is no execution state machine to port the reject-stops-the-run rule onto until Phase 8, so `WorkflowBridgeApprovalTests` stays as it is until then. Renaming the workflow tables into `Execution` was also dropped: they carry workflow baggage (workflow id, version tracking, delayed resume), and the execution ledger is designed fresh in Phase 8.

What step B did instead is the part that blocks deleting workflows: **`ApprovalRequest` no longer needs a workflow.** `WorkflowInstanceId` and `WorkflowId` are nullable. A new optional `Subject` (≤ 300 chars, capped rather than rejected) says what is being decided. The dead `ElsaBookmarkId`, which nothing called, is gone. `PendingApprovalDto`, the approver-notification job (payload and handler) and the requester notification are null-safe. The Confirmations page titles a request by subject, then workflow name, and hides "View Details" when there is no run. Migration: `20260919213208_DetachApprovalsFromWorkflows`. Left for Phase 7, where a producer exists to design against: action-hash binding, an expiry status, requester ≠ approver, a concurrency token.

Verified: Domain 187, Application 59, Infrastructure 212, Api subset (Security, Middleware, Services, Approval*, RegressionTests, PostgresIntegrationTests) 265 after a full rebuild, Vitest 68/68, `tsc` clean. Four new Api tests cover a request with no workflow being listed, approved, rejected, and the notification job tolerating it. **All 45 migrations were applied from scratch to a PostgreSQL 16 container with `dotnet ef database update`; the new one was rolled back and re-applied.** Caveat: `Down` fails once a request with no workflow exists, because it re-imposes NOT NULL on the two links.

**Two things learned on the way.** (1) The real-Postgres tests (`PostgresIntegrationTests`) do catch a model that has moved ahead of its migrations: with a stale test binary the approvals endpoint returned 500 (`column a.Subject does not exist`). Rebuild the whole solution before running them after a migration change. (2) **Not fixed: `dotnet ef migrations script --idempotent` produces SQL that does not run.** Two historical migrations, `20260625123421_EnrichAssistantDefinition` and `20260827122640_AddUserPasswordChangedAt`, each contain a raw `UPDATE` with no trailing `;`, and EF's idempotent wrapper needs one inside its `IF … THEN … END IF`. With a `;` added to those two statements in a scratch copy the whole script applies (45/45). `dotnet ef database update`, which `RUNBOOK.md:121` uses, is unaffected, and nothing in CI/CD uses the script. It matters wherever a DBA has to review or apply generated SQL, which regulated deployments often require. The fix is a `;` in each of those two `migrationBuilder.Sql` strings and changes nothing at runtime.

---

## 6. UI gap table (brief Part 65) — from `roleNav.ts` and `router.tsx`

| Current UI | Problem | Target | Action | Pri |
|---|---|---|---|---|
| Build → "Automations" (`/automations`, canvas builder), shown to Admin, SuperAdmin *and* User | workflow-first primary nav (brief Part 25) | out of nav; route labelled legacy until Elsa is deleted | isolate → delete | P0 |
| Build → "Chatbots" | separate noun for what is an assistant's embed channel | channel tab of AI Assistant | MODIFY | P1 |
| Build → "Integrations"; SuperAdmin-only "Tools & APIs" | two nouns for one concept; raw "tool" wording | "Tools & MCP" plus Capabilities | MODIFY | P0 |
| Capabilities live only as a tab inside `/workspaces/:id` | the central concept is hidden | top-level Capabilities page (filter by system, risk, transport, status) | ADD | P0 |
| Test → "Playground" hosts `AutomationPlaygroundPane` | workflow-centric | system → assistant → instruction → resolved capability, inputs, policy, risk, result | MODIFY | P0 |
| Operate → "Runs" (`RunInspectorDrawer`) | runs are workflow runs | "Executions" | REPLACE | P0 |
| Operate → "Approvals" | workflow-approval language | "Confirmations" | REPLACE | P0 |
| Operate has no Conversations | brief separates conversations from executions | Conversations page | ADD | P1 |
| Manage → "Connected Systems" → `/workspaces` | label already right; route and code still say workspace/application | keep label, rename route later | MODIFY (cosmetic) | P2 |
| Dashboard `HomePage` (22 workflow/automation mentions) | not built around "Ask R2WAI" | ask box, system/knowledge/capability counts, activity, pending confirmations, health | MODIFY | P1 |
| End-user menu: "AI Assistant" → `/playground`, "My Activity" → `/runs` | end-user chat lives in the admin playground | a real assistant screen with result cards and confirmation | MODIFY | P0 |

Not assessed today: responsive behaviour, accessibility, widget UI → **UNVERIFIED**.

---

## 7. Assumptions made (nothing was asked)

1. The new brief supersedes the audit's "keep Elsa and SK" advice.
2. **Automations ends as a removed feature, including scheduled and webhook-triggered automations, which die with Elsa.** If you want those kept as "scheduled assistant tasks", say so before step C.
3. No production data exists, so dropping Elsa tables and reshaping `WorkflowInstance` / `ApprovalRequest` is acceptable; R2WAI's historical migrations stay.
4. Nothing is committed.

## 8. Unverified

- Elsa's schema and table names in a live database.
- Whether `Microsoft.Agents.AI` 1.22.0 is compatible with the Microsoft.Extensions.AI version chosen for .NET 10 (check when the package is first added, at step E/F).
- Whether `lib/chat/responseCards` already matches the result-card contract.
- Everything not executed in §2, including the full `R2WAI.Api.Tests` run and Playwright.
