# R2WAI Backend Audit — R2WAI 2.0

**Audit date:** 2026-10-03
**Scope:** `C:\Users\LENOVO\Bots\R2WAI` — complete backend. Read-only static inspection plus a locally executed Release restore/build/test baseline.
**Method:** Full source read of `src/R2WAI.{Api,Application,Domain,Infrastructure}` and `tests/`, plus executed `dotnet` commands. No code was modified by this audit.

**Classification used throughout:**
- **VERIFIED** — directly present in source/config, or observed in an executed command.
- **INFERRED** — reasoned from verified facts, explicitly labelled.
- **UNKNOWN** — not established. No claim is made.

> **Correction to the prior draft of this document:** the earlier version stated the full `dotnet test R2WAI.slnx` run "succeeded … 5m25s". That was not observed. The full solution-wide run **exceeded a 30-minute timeout and never reported a result**. The real numbers below come from segmented runs, which do complete. Do not treat "the whole suite passes in one command" as an established fact.

---

## 1. Baseline (executed commands, this session)

| Command | Result | Detail |
|---|---|---|
| `dotnet --version` | VERIFIED | `10.0.204` (also installed: 8.0.425, 10.0.203) |
| `dotnet restore R2WAI.slnx` | VERIFIED, success | All projects up to date. `NU1903` high-severity advisories on transitive `System.Security.Cryptography.Xml` 10.0.7 and `SSH.NET` 2024.2.0. |
| `dotnet build R2WAI.slnx -c Release --no-restore` | VERIFIED, success | **Incremental** build: 12 warnings, 0 errors, 3.7 s (misleading — most projects were already built). |
| `dotnet build R2WAI.slnx -c Release --no-incremental` | VERIFIED, success | **315 warnings, 0 errors, 52 s.** This is the real number. |
| `dotnet test R2WAI.slnx -c Release --no-build` | **DID NOT COMPLETE** | Timed out at 30 min with no result line. See note above. |
| `dotnet test` per segment (Domain/Application/Infrastructure + 4 Api.Tests segments) | VERIFIED, success | **1,199 passed, 0 failed, 0 skipped.** |

### Test results by project (segmented, all verified passing)

| Project | Passed | Failed | Skipped | Wall clock |
|---|---|---|---|---|
| `R2WAI.Domain.Tests` | 200 | 0 | 0 | ~0.1 s |
| `R2WAI.Application.Tests` | 124 | 0 | 0 | ~0.4 s |
| `R2WAI.Infrastructure.Tests` | 343 | 0 | 0 | ~0.3 s |
| `R2WAI.Api.Tests` | 532 | 0 | 0 | ~11 min (4 segments) |
| **Total** | **1,199** | **0** | **0** | |

Api.Tests segments: `Security` 187 · `Controllers` 168 · `Integration` 114 · `Logging`+`Middleware`+`Services`+`Workflows`+`StartupDiagnostic` 63.
Docker was available (Server 29.7.2); the 10 Testcontainers PostgreSQL test classes ran and passed.

### Build warning breakdown (`--no-incremental`, 315 total incl. per-project repeats)

| Code | Count | Meaning |
|---|---|---|
| `IDE0290` | 258 | Use primary constructor (style, from `.editorconfig`) |
| `CS8618` | 120 | Non-nullable property uninitialized (mostly test fixtures / DTOs) |
| `IDE0161` | 120 | Convert to file-scoped namespace |
| `NU1903` | 48 | Vulnerable package advisory (7 distinct advisories) |
| `CS0618` | 38 | Obsolete member — consistent with the Elsa 3.7 API surface |
| `CS8604/8613/8767/9113/8603/8600/8602` | 34 | Nullable-analysis warnings |
| `IDE0040` | 4 | Add braces |

**No compiler errors. No analyzer packages are installed** — only built-in CA rules at `AnalysisLevel=latest` plus IDExxxx style enforcement (`Directory.Build.props:6-8`).

---

## 2. Solution, projects, and layering

**VERIFIED.** Two solution files exist, and they disagree:

| File | Projects |
|---|---|
| `R2WAI.slnx` (root, used by CI) | 4 src + 4 test = **8** |
| `src/R2WAI.slnx` | 4 src + 3 test = **7** — **`R2WAI.Api.Tests` is missing** |

`.github/workflows/ci.yml:29-33` and `:158-161` call this divergence out explicitly. CI uses the root solution, so the 532 API tests **do** run in CI. Anyone opening `src/R2WAI.slnx` in an IDE silently loses the entire API test project.

**VERIFIED — layering is clean:**

| Project | References |
|---|---|
| `R2WAI.Domain` | **nothing** (0 ProjectReferences; only `MediatR 12.4.1`, used once in `Domain/Common/DomainEvent.cs:1`) |
| `R2WAI.Application` | → Domain only |
| `R2WAI.Infrastructure` | → Application, Domain |
| `R2WAI.Api` | → Application, Infrastructure (deliberate; injects `ApplicationDbContext` directly in 4 controllers) |

No source file in `R2WAI.Application` contains `using R2WAI.Infrastructure` — VERIFIED. The dependency rule is respected.

**VERIFIED — target framework** `net10.0` on all 8 projects (`Directory.Build.props:3`; redundantly re-declared in every csproj).

**VERIFIED — `global.json` does not exist.** The SDK is unpinned. CI pins `DOTNET_VERSION: '10.0'` (`ci.yml:10`, `cd.yml:9`); `docker/Dockerfile.api:2` uses `sdk:10.0` / `aspnet:10.0`. Local builds resolve whatever 10.x is installed.

**VERIFIED — `LangVersion` is unset.** Effective version is the SDK default for `net10.0`.

**VERIFIED — duplicate/unused package references** (declared but not used in source):

| Project | Package |
|---|---|
| `R2WAI.Api.csproj:13` | `Microsoft.EntityFrameworkCore.Design` — the only `.Design` consumer is `Infrastructure/Persistence/DesignTimeDbContextFactory.cs`, in a project that does **not** declare it |
| `R2WAI.Api.csproj:14` | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` — no `AddDbContextCheck`; `DatabaseHealthCheck.cs:8` says it is deliberately replaced |
| `R2WAI.Infrastructure.csproj:26` | `Microsoft.AspNetCore.Authentication.JwtBearer` — redundant with `FrameworkReference Microsoft.AspNetCore.App` (line 9) |
| `R2WAI.Infrastructure.csproj:31` | `Microsoft.Agents.AI.OpenAI` — no source file imports `Microsoft.Agents.AI.OpenAI` |
| `tests/…Api.Tests.csproj:13` | `Microsoft.EntityFrameworkCore.InMemory` — 0 usages |
| `tests/…Application.Tests.csproj:12` | `Microsoft.Extensions.DependencyInjection` — 0 usages |
| `tests/…Domain.Tests.csproj:13` | `Moq` — 0 usages |

### Verified inventory

| Metric | Value |
|---|---|
| Controllers in `src/R2WAI.Api/Controllers` | **26** |
| Feature folders in `R2WAI.Application/Features` | **17** |
| Entities in `R2WAI.Domain/Entities` | **47** |
| `DbSet<>` declarations in `ApplicationDbContext` | **89** |
| EF Core migrations (`*.cs`, excl. Designer/snapshot) | **60** |
| Model snapshot | `ApplicationDbContextModelSnapshot.cs` present |
| `BackgroundService` / `IHostedService` implementations | **4** |

---

## 3. Domain model — VERIFIED

### 3.1 There is no `Workspace`. At all.

**VERIFIED.** No `Workspace.cs`, no class named `Workspace`, no `DbSet<Workspace>`. A case-sensitive search for the exact token `WorkspaceId` across `src`, `docs`, and `tests` returns **zero matches**. `/workspaces` exists only as a *React client route* for "Connected Systems" (`src/R2WAI.Client/src/lib/nav/roleNav.ts:55`, `app/router.tsx`).

**Consequence:** the target security hierarchy `Tenant → Workspace → Resource` is **not implementable by configuration today**. It requires new schema, new authorization plumbing, and migration of every workspace-owned resource. Nothing in the current codebase enforces a workspace boundary.

### 3.2 Current ownership boundary is `TenantId` only

**VERIFIED.** `BaseEntity<Guid>` (`Domain/Common/BaseEntity.cs:5-42`) provides `Id`, `CreatedAt`, `ModifiedAt`, `IsDeleted`, `DomainEvents`. Most entities add their own `Guid TenantId`.

**10 entities have no `TenantId`:** `Tenant` (root), `AccessRequest` (pre-provisioning), `BackgroundJob`, `KnowledgeBaseSource`, `MessageAttachment`, `TestCaseResult`, `WorkflowStepExecution`, `UserRole`, `AssistantBehaviorSettings`. The last six are children reachable only through a `TenantId`-bearing parent — acceptable.

**Critical gap — `BackgroundJob`:** `BackgroundJob` (`BackgroundJobs/BackgroundJob.cs:14-32`) has **no `TenantId`**, and `DbSet<BackgroundJob> BackgroundJobs` (`ApplicationDbContext.cs:127`) exists on the concrete class but is **absent from `ITenantDbContext`** (`ApplicationDbContext.cs:10-57`). The global tenant filter is applied reflectively to any type with a `Guid TenantId` property (`ApplicationDbContext.cs:138-172`), so `BackgroundJob` gets **no tenant filter at all**. A durable job queue with no tenant column means job payloads are the only tenant discriminator — see §5.4.

### 3.3 `Department` and the domain-`Application` concept

**VERIFIED — this is the entanglement the migration must unpick.**

`Department` (`Domain/Entities/Department.cs:5`) is a real, FK-bearing, seeded entity:

- `ConnectedApplication.DepartmentId` (`ConnectedApplication.cs:20`) — **non-nullable**, FK to Department
- `ModelConfiguration.DepartmentId` (`ModelConfiguration.cs:10`) — FK to Department
- `AccessRequest.Department` (`AccessRequest.cs:11`) — free-text signup field, not an FK
- `ApplicationDbContextSeed.cs` seeds Departments, then Applications under them, then cross-tenant demo data
- `DepartmentsController.cs` — full CRUD, reads open to any authenticated user, writes `Admin,SystemAdmin`

The **working tree already contains uncommitted work relaxing this**: `20261001082539_MakeConnectedApplicationDepartmentOptional` makes `ConnectedApplication.DepartmentId` nullable, plus `20261001094300_AddDepartmentlessApplicationCodeUniqueness` and `20261001192826_MakeAccessRequestDepartmentOptional`. **These must not be discarded or duplicated.**

`ConnectedApplication` is the *domain* `Application`, mapped to `DbSet<ConnectedApplication> Applications` on table `Applications` (`ApplicationDbContext.cs:87`). The class doc (`ConnectedApplication.cs:7-10`) states `Application` was avoided as a class name because it collides with the `R2WAI.Application` namespace.

Entities with `ApplicationId`: `ApplicationApi`, `ApplicationConfiguration`, `ApplicationVersion`, `NavigationDefinition` (**non-nullable**), `AssistantDefinition`, `KnowledgeBase`, `ModelConfiguration`, `TestCase`, `TestRun`, `ToolDefinition`, `Workflow`, `AuditLog` (all nullable).

**Mapping already in progress (INFERRED, strongly supported):** `ConnectedApplication` is functioning as a **connector definition** (base URL, env, `ApplicationApi` with auth scheme + encrypted credential + OpenAPI source, `ApplicationConfiguration` with model/timeout/RAG threshold, `ApplicationVersion` snapshots). That is precisely the target `Connection` concept. It is **not** the product-level "Application" the 2.0 spec wants to eliminate. **This is the single most important classification decision in the migration and it must be confirmed with the product owner before any rename.**

### 3.4 `AssistantDefinition` is the de-facto Agent

**VERIFIED.** `AssistantDefinition` (`Domain/Entities/AssistantDefinition.cs:6-29`) holds `Name`, `Description`, `Type`, `SystemPrompt`, `ModelConfigurationId`, `KnowledgeBaseId`, `Tools`, `Settings`, `IsActive`, `PublishStatus`, `PublishedVersion`, `PublishedAt`, `Tags`, `AvatarUrl`, `UsageCount`. It already has `AssistantVersion` (config snapshot + `IsPublished` + `PublishedByUserId` + `PublishedAt`) and a `PublishConcurrencyToken` migration.

This maps almost 1:1 to the target `Agent` / `AgentVersion`. **Recommendation: rename, do not re-create.** Creating a parallel `Agent` entity would duplicate `AssistantDefinition` + `AssistantVersion` + `PublishedAssistantsController` + the whole client feature folder.

### 3.5 No `Connection`, `ConnectionCredential`, `ConnectionOperation`, `AgentPublication`, `AgentExecution`, `ToolExecution`, `KnowledgeExecution` entities exist

**VERIFIED.** Zero matches for `ConnectionCredential` across `src`, `docs`, `tests`.

The closest existing concepts:

| Target entity | Nearest existing | Status |
|---|---|---|
| `Connection` | `ConnectedApplication` + `ApplicationApi` (split) | **Split across two entities** — `ConnectedApplication` = definition, `ApplicationApi` = connection/operations |
| `ConnectionCredential` | `ApplicationApi.CredentialSecretEncrypted`; `McpServerConnection.CredentialEncrypted`; `ToolDefinition.Configuration` (JSON); `ChatbotChannel.EncryptedCredentials` | **No unified credential entity — 4 different storage mechanisms** |
| `ConnectionOperation` | `ApplicationApi` (one per application) / `ToolDefinition` (one per operation) | Split |
| `ToolDefinition` | `ToolDefinition` + `ToolDefinitionVersion` | **Exists and is governed** |
| `AgentVersion` | `AssistantVersion` | Exists |
| `AgentPublication` | `AssistantDefinition.PublishStatus` + `PublishedAssistantVersionId` + `Chatbot.PublishedAssistantVersionId` | Exists as columns, not an entity |
| `AgentExecution` / `ToolExecution` / `KnowledgeExecution` | `Conversation` + `Message`; `WorkflowInstance` + `WorkflowStepExecution`; `TestRun` + `TestCaseResult`; `AuditLog` | **No unified execution ledger.** No correlation between an agent chat and the tools it called. |
| `ModelProvider` / `ModelConfiguration` | `ModelConfiguration` exists (per-tenant, encrypted key, `DataClassification`) | Exists; provider selection is config-driven, not entity-driven |

---

## 4. Identity and authorization — VERIFIED

### 4.1 Authentication

| Mechanism | Status | Evidence |
|---|---|---|
| JWT bearer | **Active** | `Program.cs:109-144`; HS256; issuer/audience/lifetime validated; `ClockSkew = TimeSpan.Zero`; claims include `tenant_id`, `jti`, `iat`, one role claim per role (`JwtService.cs:20-60`) |
| Refresh tokens | **Active** | 64 random bytes, SHA-256+base64 at rest (`JwtService.cs:136-153`); expired-token path re-checks the HS256 algorithm (`:121-125`) |
| API keys | **Active, custom middleware** | `ApiKeyAuthenticationMiddleware.cs` — static config keys (`:119-138`) and DB keys (`:57-76`); `FixedTimeEquals`; scope enforcement (`:101-117`) |
| TOTP MFA | **Active** | Setup/enable/disable/status (`AuthController.cs:605-686`); restricted-token enforcement via `MfaSetupScopeMiddleware.cs` |
| Entra ID SSO | **Active** | `AuthController.cs:688-729` → `EntraIdAuthService`; user looked up by email with `IgnoreQueryFilters()` |

**`app.MapControllers().RequireAuthorization()` (`Program.cs:422`)** makes authorization default-deny. 16 explicit `[AllowAnonymous]` opt-outs, pinned by `tests/…/Security/AnonymousSurfaceTests.cs`.

### 4.2 Two independent tenant-resolution mechanisms that can disagree — VERIFIED

- `TenantResolutionMiddleware.cs:23` reads the JWT `tenant_id`; `:26-29` falls back to header `X-Tenant-Id`; `:31-34` stores the result **only** in `HttpContext.Items["TenantId"]`.
- `TenantAuthorizationFilter.cs:17-44` compares the header against the claim and 401s on mismatch.
- **`CurrentUserService.TenantId` (`Infrastructure/Services/CurrentUserService.cs:23-30`) reads only the `tenant_id` claim** and never reads `Items` or the header. Since `ApplicationDbContext.TenantId` delegates to it (`ApplicationDbContext.cs:69`), **the `X-Tenant-Id` header has zero effect on data access.**

Not a bypass (the header can only fail the check, never widen it), but the header is dead code and the two mechanisms can silently disagree. Pick one.

### 4.3 RBAC is 3 roles — VERIFIED

`Program.cs:146-166` defines `AdminOnly`, `TenantAccess`, `CanManageUsers`, `CanManageDocuments`, `CanManageWorkflows`. All are `RequireRole("Admin","SystemAdmin")` or a `tenant_id` claim assertion. `Program.cs:155-157` records that `SuperAdmin`, `Editor`, `Contributor`, `WorkflowManager`, `UserManager` were retired on 2026-08-29 (migration `20260829091112_CollapseRbacToThreeRoles`).

**VERIFIED dead code — resource-level permissions do not exist.** `Role.Permissions` (`Role/Permissions` string) is written by `CreateRoleCommand.cs:38`, `UpdateRoleCommand.cs:36`, and seeded. `User.HasPermission(Permission)` (`User.cs:167-177`) parses and evaluates it. **`HasPermission` has exactly one occurrence in the entire repository: its own declaration.** The migration itself says so at `20260829091112_CollapseRbacToThreeRoles.cs:90`: *"`User.HasPermission()` (its only reader) is dead code"*.

**Consequence:** editing a role's permissions via the admin API changes nothing. All effective authorization is coarse role-name matching. The `Permission` enum in the spec's target model does not exist.

### 4.4 Controllers with `[Authorize]` and no role check — VERIFIED

These 19 controllers require authentication but apply **no role or permission check at any level**:

`ApplicationsController`, `ApprovalsController`, `IntegrationsController`, `NavigationController`, `McpConnectionsController`, `KnowledgeBasesController`, `TestCasesController`, `TestRunsController`, `ChatbotsController`, `AssistantsController`, `DocumentsController`*(policy-gated)*, `RunsController`, `WorkflowsController`*(policy-gated)*, `UserManagementController`*(policy-gated)*, `ApiKeysController`, `NotificationsController`, `BusinessCapabilitiesController`, `PublishedAssistantsController`, `ChatController`.

Note `ApiKeysController` **does** carry `[Authorize(Roles = "Admin,SystemAdmin")]` at class level (`ApiKeysController.cs:12`) — but `IntegrationsController.cs:17` and `McpConnectionsController.cs:22` do not, and those two handle MCP credentials and integration secrets.

### 4.5 Audit — VERIFIED

`AuditLog` (`AuditLog.cs:6-24`), table `AuditLogs`, `jsonb` Old/New values, composite indexes on `(TenantId, Timestamp)` and `(TenantId, EntityType, EntityId)`.

Two write paths:
1. **Automatic** — `ApplicationDbContext.SaveChangesAsync` (`:175-215`) snapshots every Added/Modified/Deleted `BaseEntity<Guid>`; `OnAfterSaveAudit` (`:262-286`) inserts one row per entry.
2. **Explicit** — 17 `new AuditLog(...)` call sites (ToolGateway, ApprovalService, controllers, handlers).

**Redaction list `SensitiveAuditFields` (`ApplicationDbContext.cs:217-222`) is incomplete.** It matches on **property name only** and contains `SecretHash` — a property that **does not exist anywhere in the entity model** — while omitting `Secret` (`WebhookEndpoint.cs:12`, stored in plaintext) and the three `*Encrypted` credential properties `ApplicationApi.CredentialSecretEncrypted`, `McpServerConnection.CredentialEncrypted`, `ChatbotChannel.EncryptedCredentials`.

---

## 5. AI runtime, tool gateway, knowledge, automations

### 5.1 Two runtimes exist; only one is used — VERIFIED

| Item | Status |
|---|---|
| `IAgentRuntime` (`Application/Common/Interfaces/IAgentRuntime.cs:35`) | **Registered, never called.** |
| `AgentRuntime` (Semantic Kernel impl) | Never invoked |
| `AgentFrameworkRuntime` (Microsoft.Agents.AI impl, `AI/AgentFrameworkRuntime.cs:36`) | Never invoked |
| `AgentRuntimeSelector` (`AI/AgentRuntimeSelector.cs:17`) | Never invoked |
| `AgentRuntimePolicyService` | Reads a tenant `GlobalPolicy` of type `AgentRuntime`. **No runtime effect.** |
| `IAIService` → `SemanticKernelService` | **This is what every live endpoint uses.** |

`IAgentRuntime` appears **only** in: its own declaration, the two implementations, the selector, the policy interface, and `Infrastructure/DependencyInjection.cs:117-118`. **Zero production call sites.** `WorkflowsController.cs:63-65` contains a comment describing `IAgentRuntime`'s behaviour "as a code path" that no endpoint uses.

`AgentFrameworkRuntime.cs:70-90` builds its `OpenAIClient` directly from `AI:OpenAI:*` config — no tenant model resolution, no Ollama/Z.ai support, no history truncation. It is not a production-ready runtime.

**Per section 11 of the brief: migration to Microsoft Agent Framework is NOT complete and must not be claimed.** Two runtimes coexist; only Semantic Kernel is live.

### 5.2 Model providers — VERIFIED

`IModelProvider` implementations registered at `Infrastructure/DependencyInjection.cs:89-91`:

| Provider | Config keys | Chat | Embeddings |
|---|---|---|---|
| `OpenAiModelProvider.cs:27-43` | `AI:OpenAI:ApiKey`, `AI:OpenAI:ModelId` (default `gpt-4o`) | yes | yes (`text-embedding-3-small`) |
| `OllamaModelProvider.cs:28-40` | `AI:Ollama:Endpoint` (**throws if missing**), `AI:Ollama:ModelId` (default `qwen3:4b`) | yes | yes |
| `ZaiModelProvider.cs:31-37` | `AI:ZAI:ApiKey` (**throws if missing**), endpoint default `https://integrate.api.nvidia.com/v1` | yes | **no** |

`ModelGateway.cs:14-45` keys providers by name from `AI:Provider` (default `openai`), with `AI:FallbackProvider` as a one-shot retry. `ModelConfigurationResolver.cs:22-63` resolves the tenant's `IsDefault && IsActive` `ModelConfiguration`, rejects cross-tenant/inactive, re-checks `DataClassification`, and decrypts `ApiKeyEncrypted`.

**There is no `IChatModel` / `IEmbeddingModel` interface.** The seam is `IModelProvider.ConfigureKernel`. Capability declaration is implicit per provider, not an explicit capability flag — so "never assume capabilities" is not yet structurally enforced.

### 5.3 Tool gateway — VERIFIED, and genuinely centralized

`ToolGateway.InvokeAsync` (`Infrastructure/AI/ToolGateway.cs:63-180`) is a single runtime-agnostic enforcement point. Both AI SDKs route through it: Semantic Kernel via `AiFunctionAuditFilter.cs:50-72`, Microsoft Agents AI via `MafToolFunctionFactory.cs:76-87`.

**Fail-closed decision order:**

| # | Check | Line |
|---|---|---|
| 1 | Unknown tool (no `ToolDefinition`, no built-in record) → deny | `:102` |
| 2 | `RequiredRole` missing from caller → deny | `:104-106` |
| 3 | `ApprovalRequired` → deny / defer | `:108` |
| 4 | Tenant `ToolExecution` policy risk ceiling exceeded → deny | `:79-84` |
| 5 | Tenant `Approval` policy `requireApprovalAboveRiskLevel` → deny | `:86-91` |
| 6 | `IsEnabledForCallingAssistant` re-check → deny | `:93-97` |

Then execute → metrics → trace → audit → progress. Denials are messages to the model, not exceptions. Execution exceptions propagate **after** audit.

**No model-generated tool call reaches an external system without passing this gateway.** That requirement of the brief is **met** in the current codebase.

### 5.4 Knowledge / RAG — VERIFIED, with a structural gap

`PgVectorService.cs` uses raw SQL against `vector_embeddings` (`:44-52`). **The table has no `tenant_id` column.** Tenant isolation depends entirely on `collection_name`, which is `kb_{KnowledgeBase.Id:N}` (`KnowledgeBaseService.cs:43`).

`IVectorStoreService` methods **never receive a tenant id** — only a `collectionName`. So the vector layer provides **zero** tenant defense of its own; it is correct only because every caller resolves the `KnowledgeBase` through the tenant-filtered context first. `DocumentService.cs:210-216` is the tightest example: it re-resolves the collection name from a `Document` and deletes by it.

**Retrieval authorization is tenant + classification-ceiling only.** `KnowledgeBaseService.SearchKnowledgeBaseAsync:272-361` applies a tenant `Knowledge` policy `DataClassification` ceiling (`:293-311`) and resolves a RAG threshold (`:327`). **There is no per-user, per-role, or per-document chunk-level authorization.** `SearchResultDto` carries only content/score/source.

Two ingestion paths with **divergent chunking**: `KnowledgeBaseService.AddSourceAsync:125-127` honours the KB's `ChunkSize`/`ChunkOverlap`; `DocumentService.cs:91` hardcodes `1000/200`.

Two **silent degradation** paths return empty results rather than an error: a classification-ceiling breach (`:310`) and any vector-store failure (`:342-345`). The assistant then answers ungrounded with no user-visible signal.

`AgenticRetrievalOrchestrator.cs` adds a bounded rewrite loop (max 2 iterations, sufficiency threshold 0.80).

### 5.5 Automations — VERIFIED: a real durable engine with a documented workaround

Elsa 3.7.0 is genuinely wired (`Program.cs:63-95`): workflow management, EF Core PostgreSQL persistence, HTTP activities, scheduling, email, `RunMigrations`.

`WorkflowBridge.cs` builds a Flowchart **republished per run**, and R2WAI keeps its own `WorkflowInstance` + `WorkflowStepExecution` rows as the durable source of truth.

**Documented structural limitation — `WorkflowBridge.cs:247-260`:** Elsa 3.7.0/3.7.1 bookmark resume is recorded as broken for these per-run-republished Flowcharts (verified against Elsa's own persisted state). The workaround is to never resume the same run — mark the step complete in R2WAI's table and start a **brand-new** Elsa run with only the remaining steps.

State is genuinely recoverable: `WorkflowInstance.PendingResumeAt` / `PendingResumeStepIndex` / `PendingResumeLeaseExpiresAt`, `WorkflowStepExecution.AttemptCount` (row reused across retries), and an atomic lease claim in `WorkflowDelayResumeBackgroundService.cs:92-103`.

**Do not introduce a second workflow engine.** This one is durable, tested (6 test files), and production-shaped.

### 5.6 Background jobs — VERIFIED

`BackgroundJobProcessor.cs`: 5 s poll, batch 20, 5 min lease. Claimable-job predicate is built once as an `Expression<Func<...>>` (`:79-85`) so the SELECT and the claim UPDATE cannot disagree; the claim is an atomic `ExecuteUpdateAsync` (`:97-103`) and affected-rows==0 is skipped — **multi-replica safe**.

Handlers registered: `NotifyApproversJobHandler`, `IndexDocumentJobHandler`.

**Background context problem:** `CurrentUserService` derives `UserId`/`TenantId`/`Roles` purely from the ambient `HttpContext` JWT claims (`CurrentUserService.cs:14-41`) and **nothing is settable programmatically**. Every background path therefore has to bypass the tenant filter and substitute its own check. There are **41 `IgnoreQueryFilters()` call sites**. Most re-apply an explicit tenant predicate with a comment explaining why (good). The systemic risk: `BackgroundJob` has **no `TenantId`**, so nothing validates a queued job row's own tenant against the tenant in its payload.

---

## 6. Test suite assessment — VERIFIED

1,199 tests, all passing. Structure is genuinely good: `Security/`, `Integration/`, `Middleware/`, `Services/`, `Controllers/`, plus per-layer suites. `TenantIsolationFailClosedTests`, `AnonymousSurfaceTests`, `ToolGovernanceFilterTests`, `EgressGuardTests`, `IntegrationCredentialCodecTests`, `EncryptionServiceTests` are the right tests to exist.

### Critical test-integrity finding: silent soft-skips

**VERIFIED.** Many tests `return;` (report PASS, assert nothing) when a prerequisite fails:

```csharp
// tests/R2WAI.Api.Tests/Security/RoleMatrixSecurityTests.cs:31
if (client.DefaultRequestHeaders.Authorization is null) return; // login itself failed - nothing to assert
```

Counts of `return;` guards: `AuthenticatedFlowTests` 19, `PostgresIntegrationTests` 17, `PgVectorStoreRegressionTests` 15, **`RoleMatrixSecurityTests` 12**, `ApiInputBoundaryTests` 4, `JwtSecurityTests` 4, `ApprovalDecisionRaceRegressionTests` / `BackgroundJobLeaseReclaimRegressionTests` / `SweeperClaimRegressionTests` / `WorkflowDelayResumeLeaseReclaimRegressionTests` 3 each, `BackgroundJobEnqueueRegressionTests` / `ChatConcurrencyRegressionTests` / `ContextMemorySummarizationRegressionTests` 2 each.

**Every negative authorization test in `RoleMatrixSecurityTests` (all 12) asserts nothing if login fails.** This file is the only negative role-matrix coverage in the repository. Today the seeded accounts exist (`ApplicationDbContextSeed.cs:61,74,89` → `admin@r2wai.io`, `user@r2wai.io`, `deptadmin@r2wai.io`), so the tests presumably do assert — but nothing enforces that, and a seed or password change converts 12 security tests into green no-ops **with no build break**.

**Recommendation:** convert these to `Assert.Skip` (xUnit 2.9.3 supports `Assert.Skip` on a live skip) or, better, make login failure an assertion failure. At minimum, the security suites must never silently pass.

### Known-stale comment

`IntegrationTestBase.cs:96` sets `ENCRYPTION_KEY` to a **literal base64 key in source** via `Environment.SetEnvironmentVariable`. It is a test-only key, but it is a committed secret-shaped literal and a global process mutation that leaks across test collections.

---

## 7. Deployment and operations — VERIFIED present, UNKNOWN executed

| Area | Status |
|---|---|
| Docker | `docker/Dockerfile.api`, `Dockerfile.client`, 3 compose files, `nginx/studio.conf`, `monitoring/prometheus.yml`, `backup.sh`, `restore.sh` |
| Kubernetes | `k8s/`: `api-deployment`, `studio-deployment`, `ingress`, `configmap`, `namespace`, `networkpolicy`, `redis`, `kustomization` |
| CI/CD | `.github/workflows/ci.yml`, `cd.yml` |
| Env templates | `docker/.env.example`, `.env.production.example` |
| Telemetry | OpenTelemetry console + OTLP + Prometheus (`Program.cs:274-314`) |
| Health | `/health`, `/health/ready`, `/health/startup` |

**UNKNOWN:** no deployment, migration rollout, smoke test, backup, restore, or rollback was executed or verified. No CI run history was queried. **A pipeline definition is not evidence of a successful release.**

---

## 8. What must NOT be believed

Stated plainly so it is not carried forward as fact:

1. ❌ **"The full test suite passes in one command."** It timed out at 30 min. Segmented runs pass.
2. ❌ **"Semantic Kernel → Microsoft Agent Framework migration is complete."** The MAF runtime has **zero call sites**.
3. ❌ **"Fine-grained permissions (Agent.Read, Agent.Publish, …) are enforced."** No such permission model exists; `HasPermission` is dead code.
4. ❌ **"Workspace isolation is in place."** There is no `Workspace` entity, property, or reference in the backend.
5. ❌ **"Tool governance is fully enforced."** `ConfirmationRequired` is written and never read.
6. ❌ **"Approval policy honours MinApprovers."** It is stored, seeded, exposed in DTOs — and never read in a decision path.
7. ❌ **"The SSRF guard is complete."** It blocks literal private IPs; it does not resolve DNS and does not constrain redirects.
8. ❌ **"Audit logs never contain credentials."** The redaction list is missing `Secret` and three `*Encrypted` properties.
9. ❌ **"No raw SQL bypasses tenant filters."** `vector_embeddings` has no tenant column at all; `BackgroundJob` has no tenant column and no filter.
10. ❌ **"Resource-level permissions are configurable."** `Role.Permissions` is write-only configuration.

---

## 9. UNKNOWNS requiring environment access

These cannot be resolved from the repository:

1. Live database migration level, row counts, and actual `Department`/`Application` usage per tenant.
2. Which API clients (React app, widget, external integrators) depend on which routes.
3. Which model providers and connectors are actually configured in each environment.
4. Whether production `pgvector` HNSW indexes exist (`PgVectorService.cs:70-86` treats index creation as non-fatal).
5. MinIO bucket policy, Redis deployment, and backup/restore status.
6. CI execution history and scan results.
7. `EntraIdAuthService.cs` issuer/tenant/audience pinning strictness, and JIT-provisioning behaviour when no matching user exists.
8. Whether `VersionSnapshot` builders can capture an encrypted secret into a plaintext snapshot.
9. `EscalationBackgroundService` / `DataRetentionBackgroundService` internals (registration confirmed, bodies not read).
10. Whether the leaked pseudonym fields (`Department`/`Application` name collisions in `ROADMAP.md` and the committed `src/R2WAI.Api/Logs/r2wai-20260820.log`) matter — note that log file contains full serialized DTO bodies including `TenantId`.

---

## 10. Recommended sequence

Ordered by evidence, not ambition. **Step 0 is not optional.**

0. **Close the verified security holes** (see `docs/security/BACKEND-SECURITY-REVIEW-2026-10-03.md`). These are pre-existing defects in working code, independent of the 2.0 domain change.
1. **Fix test integrity** — make security tests fail loudly instead of soft-skipping. Everything after this depends on trustworthy tests.
2. **Reconcile the solution files** (`src/R2WAI.slnx` drops the API test project).
3. **Decide `ConnectedApplication` → `Connection`.** This is a product decision, not a refactor. Everything in §3.3 hinges on it.
4. **Add `Workspace` additively** — new table, nullable `WorkspaceId`, no drops, backfill measured and reversible.
5. **Rename `AssistantDefinition` → `Agent`.** Do not create a parallel entity.
6. **Add the execution ledger.** Today there is no correlation between a chat, the tools it called, and the knowledge it retrieved. This is the biggest *missing* capability, not a rename.
7. **Only then** consider retiring `Department`/`Application` as separate, backup-verified releases.

---

*Produced by read-only inspection plus executed build/test commands. No product code was changed.*