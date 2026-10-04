# Backend Security Review — R2WAI

**Date:** 2026-10-03
**Scope:** `src/R2WAI.Api`, `src/R2WAI.Application`, `src/R2WAI.Domain`, `src/R2WAI.Infrastructure`, `tests/`.
**Method:** Static read of every security-relevant path. No exploit was executed; no production system was touched. Every finding cites `file:line`.

**Status convention:** `VERIFIED` = confirmed by direct code read. `UNKNOWN` = not established.

> **Bottom line:** the platform's *architecture* is sound in the places that matter most — default-deny authorization, a single centralized tool gateway, fail-closed EF tenant filters, AES-256-GCM credential encryption with key rotation. But four defects are reachable by a normal authenticated user and should be fixed before any R2WAI 2.0 work begins. Fixing them is not "rewriting working infrastructure."

---

## CRITICAL

### C-1 · SSRF: hostname resolution is never performed (DNS rebinding)

**VERIFIED.** `Infrastructure/Security/EgressGuard.cs`.

`IsAllowedUrl` blocks private addresses **only when the URL host is a literal IP address**:

```csharp
var host = uri.Host.Trim('[', ']');
if (host is "localhost" or "127.0.0.1" or ...) return false;
if (IPAddress.TryParse(host, out var ip)) { /* RFC1918 / link-local / IPv6 checks */ }
```

If `IPAddress.TryParse` fails — i.e. the host is any **DNS name** — the function falls straight through to `return true`.

Consequences:
- `http://attacker.example.com/` resolves to `127.0.0.1` → **allowed**.
- `http://attacker.example.com/` resolves to `169.254.169.254` (cloud metadata) → **allowed**.
- The suffix blocklist (`.internal`, `.local`, `.corp`, `.svc.cluster.local`) is a string check only and trivially sidestepped by a hostname that resolves into the private ranges.

The guard is applied at the correct choke points — `DynamicToolExecutor.cs:57-61`, `McpClientAdapter.cs:34-35` (discovery) and `:61-62` (call), and the KB URL fetch at `KnowledgeBaseService.cs:199-202` — so **fixing this one function fixes all four**.

`tests/…/Security/EgressGuardTests.cs` covers literal IPv4, literal IPv6, and the four blocked suffixes. **There is no test for a hostname that resolves to a private address.** The class's own doc comment (`:24-29`) explicitly flags on-premises allowlisting as an open follow-on — it does not mention that DNS is unhandled at all.

**Fix:** resolve the host to IP addresses and validate every returned address before connecting; prefer `SocketsHttpHandler.ConnectCallback` so the *connected* address is the validated one (closes the DNS-rebinding TOCTOU window between check and connect). Reject on any private/loopback/link-local/unique-local result. Add the corresponding tests first.

### C-2 · Redirects are not constrained (SSRF via 302)

**VERIFIED.** `Infrastructure/Services/ToolFramework/HttpToolClient.cs:46-52` configures the named `HttpTool` `HttpClient` with only a `User-Agent` and a 30 s timeout. No `SocketsHttpHandler` is supplied, so .NET's default `AllowAutoRedirect = true` applies, with the default 50-redirect cap.

`EgressGuard` validates the **original** URL only. A public host that answers `302 Location: http://169.254.169.254/latest/meta-data/` therefore reaches the metadata service. This applies to every governed HTTP tool call and to MCP HTTP transport (`McpClientAdapter.cs:95-104`).

**Fix:** disable automatic redirects (`AllowAutoRedirect = false`) and follow redirects manually, re-running the full egress check on each hop, with a bounded hop count.

### C-3 · `ApprovalPolicy` CRUD is open to any authenticated tenant member (privilege escalation into approve/reject)

**VERIFIED.** `Api/Controllers/ApprovalsController.cs:12` is class-level `[Authorize]` only. The policy endpoints carry no role attribute:

- `:120` `POST policies` (create)
- `:129` `PUT policies/{id}` (update — can rewrite `ApproverRoles`)
- `:138` `DELETE policies/{id}`
- `:145` `POST policies/{id}/toggle` (can disable a policy)

`ApprovalService.VerifyApproverAuthorization` (`Infrastructure/Services/ApprovalService.cs:292-314`) decides who may approve by unioning the policy's `ApproverRoles` + escalation roles and testing membership (`:310-313`).

**A plain `User`-role member can therefore set `ApproverRoles` to their own role name and then approve or reject any approval request in the tenant — including high-risk tool calls.** This defeats separation of duties, which is otherwise correctly implemented (`ApprovalService.cs:165-166`, `:224-225` check requester ≠ approver).

Note the same controller's `GET /` with an explicit `status` (`:62-75`) filters by tenant but applies **no approver check**, so any tenant member can also enumerate every approval request including `Data`, requester identity, and workflow.

**Fix:** add `[Authorize(Roles = "Admin,SystemAdmin")]` to the policy CRUD actions. Scope the `GET /` status query to requests the caller may act on.

---

## HIGH

### H-1 · `Role.Permissions` is write-only — the permission model does not exist

**VERIFIED.** `Domain/Entities/User.cs:167-177` defines `HasPermission(Permission)`. It has **exactly one occurrence in the whole repository: its own declaration.** It is written by `CreateRoleCommand.cs:38-39`, `UpdateRoleCommand.cs:36-37`, seeded, and exposed in `RoleDto`.

The migration acknowledges this explicitly: `20260829091112_CollapseRbacToThreeRoles.cs:90` — *"`User.HasPermission()` (its only reader) is dead code"*.

**Impact:** admins can edit a role's permission set through the API and observe no effect. All authorization is coarse role-name matching against 5 hard-coded policies (`Program.cs:146-166`). This is simultaneously a **functional gap** (no `Agent.Publish`-style granularity) and a **misleading-UI risk** (an admin screen implies control that does not exist).

**Fix (choose one, do not fake it):** either implement permission evaluation at the policy/handler layer, or remove the permissions UI and field. Shipping the field unenforced is the worst option.

### H-2 · Plaintext webhook secret is written into the audit log

**VERIFIED.** `WebhookEndpoint.Secret` (`Domain/Entities/WebhookEndpoint.cs:12`) is stored **in plaintext** — the only authentication credential in the model that is neither hashed (like `ApiKey.KeyHash`) nor encrypted (like `CredentialSecretEncrypted`).

The audit redaction list (`ApplicationDbContext.cs:217-222`) matches on **property name only** and contains `SecretHash` — *a property that does not exist anywhere in the entity model* — while omitting `Secret`.

**Net effect: every webhook create/update writes the live shared secret in cleartext into `AuditLogs.NewValues`.** Unlike ciphertext (H-3), this is directly usable.

**Fix:** add `Secret` and `CredentialSecretEncrypted`, `CredentialEncrypted`, `EncryptedCredentials` to `SensitiveAuditFields`; replace the non-existent `SecretHash` entry. Then encrypt the stored webhook secret at rest.

### H-3 · Audit redaction misses three encrypted-credential properties

**VERIFIED.** `ApplicationDbContext.cs:217-222` lists: `PasswordHash`, `RefreshTokenHash`, `PasswordResetToken`, `MfaSecret`, `AadhaarNumberEncrypted`, `AadhaarNumberHash`, `ApiKeyEncrypted`, `SecretHash`, `ClientSecret`, `EncryptionKey`.

Not listed, but present on live entities: `ApplicationApi.CredentialSecretEncrypted` (`:22`), `McpServerConnection.CredentialEncrypted` (`:24`), `ChatbotChannel.EncryptedCredentials` (`:12`).

Ciphertext — not plaintext — so this is not direct credential disclosure. It does defeat the redaction intent, doubles the exposure surface, and makes captured ciphertext replayable if a key ever rotates backwards.

### H-4 · TOTP shared secret stored in plaintext

**VERIFIED.** `User.MfaSecret` (`User.cs:129`) is stored in plaintext, while `AadhaarNumberEncrypted` (`:25`) and `ModelConfiguration.ApiKeyEncrypted` use AES-256-GCM in the same entity. `MfaSecret` *is* in the audit redaction list, so it is not leaked via audit diffs — but a database read yields every user's MFA seed and defeats the second factor for all of them.

**Fix:** encrypt with `IEncryptionService` (the service already exists, is AES-256-GCM, and supports rotation).

### H-5 · `ConfirmationRequired` is write-only — an unenforced tool-safety flag

**VERIFIED.** `ToolGateway.InvokeAsync` (`Infrastructure/AI/ToolGateway.cs:63-180`) checks, in order: unknown tool (`:102`), `RequiredRole` (`:104-106`), `ApprovalRequired` (`:108`), tenant risk ceiling (`:79-84`), tenant approval policy (`:86-91`), `IsEnabledForCallingAssistant` (`:93-97`).

**It never reads `ToolDefinition.ConfirmationRequired`.** The only reference anywhere under `Infrastructure/AI/` is a write at `BuiltInToolGovernance.cs:60`. Everywhere else it is persisted and surfaced in DTOs.

`ToolDefinition.DefaultGovernanceForHttpMethod` sets `ConfirmationRequired = true` for discovered DELETE/POST operations — so the admin UI shows a confirmation requirement for exactly the dangerous operations, and nothing enforces it.

**Fix:** either enforce it in the gateway (deny unless a request-scoped human confirmation is present) or delete the flag. Do not leave it displayed.

### H-6 · `ApprovalPolicy.MinApprovers` is never enforced

**VERIFIED.** Stored (`ApprovalPolicy.cs:12`), configured (`:23`, `:32`, `:40`, `:47`), migrated, seeded, and round-tripped through 9 DTO call sites in `ApprovalService.cs` (`:514`–`:615`).

**Never read in any decision path.** Multi-level chaining (`CreateNextLevelApprovalAsync`, `ApprovalService.cs:637-669`) keys off the **length of `ApproverRoles`**, ignoring `MinApprovers` entirely.

**Impact:** a tenant configures "3 approvers required"; the system approves on the first decision.

### H-7 · `vector_embeddings` has no tenant column

**VERIFIED.** `PgVectorService.cs:44-52` creates `vector_embeddings(id, collection_name, embedding, payload jsonb, created_at)`. There is no `tenant_id`. Every one of the 19 raw-SQL sites is scoped by `collection_name` only.

`IVectorStoreService` methods receive **only** a `collectionName` — no tenant id is ever passed. Isolation therefore rests entirely on the discipline of callers having resolved the owning `KnowledgeBase` through the tenant-filtered `DbContext` first.

Most do. `DocumentService.cs:210-216` re-resolves the collection name from a `Document` and deletes by it; `PgVectorService.DeleteVectorsAsync:275-278` and `DeleteCollectionAsync` swallow all exceptions after logging; `HybridSearchAsync:341-348` silently falls back to vector-only search on error, so a caller cannot distinguish a degraded hybrid result from a genuine one.

**Fix:** add `tenant_id` to `vector_embeddings`, index it, and thread tenant id through `IVectorStoreService`. Until then, treat the vector store as having no isolation of its own.

### H-8 · `BackgroundJob` has no `TenantId` and no tenant filter

**VERIFIED.** `BackgroundJobs/BackgroundJob.cs:14-32` has no `TenantId`. `DbSet<BackgroundJob>` exists on the concrete class (`ApplicationDbContext.cs:127`) but is **absent from `ITenantDbContext`** (`:10-57`).

The global tenant filter is applied reflectively to any type with a `Guid TenantId` property (`ApplicationDbContext.cs:138-172`), so `BackgroundJob` receives **no tenant filter at all**. `BackgroundJobProcessor.ProcessDueJobsAsync:87-92` sweeps with the filter effectively disabled, and each handler trusts its own payload tenant id.

Handlers do re-check (`DocumentService.cs:76-78`, `DeferredToolCallExecutor.cs:36-37`, `NotifyApproversJobHandler.cs:45`), so this is defence-in-depth rather than a live bypass — but nothing validates the queued **row's** tenant against the tenant in its **payload**.

**Fix:** add `TenantId` to `BackgroundJob`, index it, and validate payload-vs-row tenant in the processor.

---

## MEDIUM

### M-1 · Tool risk ceilings fail **open** on an unrecognised label

**VERIFIED.** `Infrastructure/AI/ToolExecutionPolicyEvaluator.cs:55-67` returns `false` ("does not exceed") when either side is unrecognised. A tenant policy containing `{"maxRiskLevel":"high-risk"}` — one character away from `"High"` — silently denies nothing.

**Fix:** fail closed on unrecognised input, or validate the policy value at write time and reject unknown labels.

### M-2 · Knowledge-policy JSON is opt-in; prose silently disables enforcement

**VERIFIED.** `ToolExecutionPolicyEvaluator.cs:29-48` and `ApprovalPolicyEvaluator.cs:24-43` require `GlobalPolicy.Content` to be a JSON object with the exact expected property. A tenant that writes a human-readable policy gets **no enforcement and no warning**.

**Fix:** reject or warn on unparseable policy content at write time.

### M-3 · Deferred tool-call replay skips governance re-checks

**VERIFIED.** `DeferredToolCallExecutor.cs:51-62` re-dispatches an approval-resumed tool call through a tenant-scoped lookup (`:36-37`) but **does not re-run the role / risk-level / policy-ceiling checks** that `ToolGateway.InvokeAsync` performs. A role revoked, or a policy ceiling lowered, between request and approval is not re-evaluated.

**Fix:** route the replay back through `IToolGateway` rather than the executor directly.

### M-4 · Approval-required only works for `Http` and `Mcp` tools

**VERIFIED.** `ToolGateway.cs:116-125` creates a deferred approval request for those two types only; `:127-130` hard-denies every other tool type with "cannot be performed automatically yet". Safe (fail-closed) but silently reduces governance coverage for other connector types as they are added.

### M-5 · Two divergent RAG ingestion paths

**VERIFIED.** `KnowledgeBaseService.AddSourceAsync:125-127` honours `KnowledgeBase.ChunkSize`/`ChunkOverlap`; `DocumentService.cs:91` hardcodes `ChunkText(text, 1000, 200)`. Same KB, different chunking depending on which path a document arrived through.

### M-6 · RAG failures degrade silently

**VERIFIED.** A knowledge-classification ceiling breach (`KnowledgeBaseService.cs:293-311`) writes an `AuditLog` and returns **empty results**; any vector-store failure (`:342-345`) logs a warning and returns zero results. Both surface to the user as an ungrounded answer with no signal that retrieval was blocked or broken.

**Fix:** return an explicit "retrieval unavailable / policy-restricted" marker to the orchestration layer rather than an empty set.

### M-7 · No chunk-level authorization in retrieval

**VERIFIED.** Retrieval authorization is tenant + knowledge-base classification ceiling only. There is no per-user, per-role, or per-document filtering. `SearchResultDto` carries content, score, and source only.

This is the requirement in §23 of the brief — *"Tenant, Workspace, Resource, Document permissions… before content enters model context"* — and it is **not met**. Today it is safe only because a `KnowledgeBase` belongs to exactly one tenant and every query is tenant-filtered. It will stop being safe the moment `KnowledgeBase` gains a `WorkspaceId` and workspaces share bases.

### M-8 · `ApiKey.KeyHash` is unsalted, unstretched SHA-256

**VERIFIED.** `Domain/Entities/ApiKey.cs:9`. Compared with `FixedTimeEquals` (no timing leak) but far weaker than `PasswordHasher`'s PBKDF2. A database leak permits offline lookup.

**Fix:** use the same hashing scheme as `User.PasswordHash`, or HMAC with the `ENCRYPTION_KEY` as `AadhaarHasher` does.

### M-9 · `IntegrationCredentialCodec` secret detection is a fixed 3-name allow-list

**VERIFIED.** `Application/Common/Security/IntegrationCredentialCodec.cs:23` encrypts exactly `Token`, `ApiKey`, `Password`. The blob is free-form JSON, so any other secret name (`clientSecret`, `privateKey`, `apiSecret`, `bearerToken`, a nested `password`) is stored and re-emitted in cleartext.

The `MergeAndEncrypt` legacy fallback (`:37-50`) treats a `CryptographicException` as "this predates encryption" and uses the value **as plaintext** — so a tampered or corrupted ciphertext is silently downgraded rather than failing.

### M-10 · No `FallbackPolicy`: hubs and static files are outside the default-deny gate

**VERIFIED.** `app.MapControllers().RequireAuthorization()` (`Program.cs:422`) applies to controllers only. SignalR hubs (`Program.cs:423-425`) and `UseStaticFiles()` (`Program.cs:413`) sit outside it. Hub authorization exists at hub level and is tested (`tests/…/Security/HubAuthorizationTests.cs`), but a new endpoint added to a hub or a static file added under the served root is unguarded by default.

### M-11 · Assistant streaming duplicates policy logic and is non-persistent

**VERIFIED.** `AssistantsController.StreamChat` (`:252-430`) bypasses MediatR entirely (`:273-274` comment) and re-implements the published gate (`:275-281`), the AI-usage cap (`:285-298`), and PII handling (`:302-324`) inline. It writes **no** `Message` rows (`:411-412`).

Two consequences: policy drift between streaming and non-streaming chat, and streamed conversations are absent from history and audit.

### M-12 · Webhook HMAC is computed over a re-serialized body

**VERIFIED.** `WorkflowsController.cs:394` re-serializes the bound model before HMAC, so byte-exact upstream signing is not preserved — a sender that signs its exact bytes will fail verification. Timestamp skew is 300 s (`:385-407`) with `FixedTimeEquals` (correct). Legacy `X-Webhook-Secret` compare (`:408-414`) is also fixed-time (correct).

---

## LOW

### L-1 · JWT secret has no non-development guard

`Authentication/Jwt:SecretKey` is accepted from config outside Development, unlike `EncryptionService`, which rejects config-file keys outside Development (`EncryptionService.cs:27-29`, `:44-46`). Inconsistent posture.

### L-2 · API-key middleware mutates `context.User` outside any authentication scheme

`ApiKeyAuthenticationMiddleware.cs` runs after `UseAuthentication()` and reassigns the principal directly. Downstream code reading `AuthenticationType` sees `"ApiKey"` rather than a scheme name. Nothing currently branches on this, but it is implicit.

### L-3 · Committed application log contains serialized DTO bodies

`src/R2WAI.Api/Logs/r2wai-20260820.log` contains EF-generated SQL including fully serialized response bodies with every `DepartmentDto`, `ApplicationDto`, and `TenantId`. Log files should be git-ignored; this one is tracked.

### L-4 · Unpinned SDK

No `global.json`. CI and Docker pin `10.0`; local builds resolve whatever is installed.

### L-5 · Security tests can pass while asserting nothing

See `BACKEND-AUDIT` §6. `RoleMatrixSecurityTests.cs:31` and 11 sibling files `return;` when login fails. All 12 negative role-matrix tests — the only negative authorization coverage in the repository — can silently become green no-ops.

---

## Verified strengths — do not regress these

Recorded so the remediation work does not damage what already works:

1. **Default-deny authorization.** `Program.cs:422`; anonymous surface pinned by tests.
2. **Centralized tool gateway.** `ToolGateway.InvokeAsync` is the single choke point for both AI SDKs. No model-generated call reaches an external system without it. This satisfies §14 of the brief.
3. **Fail-closed EF tenant filters.** `ApplicationDbContext.cs:306-330`, with the prior fail-open bug documented and regression-tested.
4. **AES-256-GCM with key rotation.** `EncryptionService.cs:74-132`; refuses config-only keys outside Development; decrypt-only previous keys.
5. **41 `IgnoreQueryFilters()` sites, nearly all re-applying an explicit tenant predicate with a written justification.** This is the mark of a codebase where isolation was fought for, not assumed.
6. **Multi-replica-safe job queue.** Shared claim expression + atomic `ExecuteUpdateAsync` + affected-rows check (`BackgroundJobProcessor.cs:79-106`).
7. **Separation of duties in approvals.** Requester ≠ approver (`ApprovalService.cs:165-166`, `:224-225`), conditional-claim race handling (`:274-285`), `FixedTimeEquals` on webhook secrets.
8. **PII redaction in logs** — `tests/…/Logging/SensitiveDataMaskingTests.cs`.
9. **Egress guard applied at all four outbound call sites**, not just the test button.

---

## Remediation order

| # | Item | Why first |
|---|---|---|
| 1 | **C-1, C-2** (SSRF) | Reachable by any user who can create a tool; reaches cloud metadata. Fix + tests. |
| 2 | **C-3** (approval-policy CRUD) | Direct privilege escalation. One-line attribute addition per action. |
| 3 | **H-2** (plaintext secret in audit) | Plaintext credential already in the audit table. |
| 4 | **H-4** (plaintext TOTP secret) | Trivial: reuse `IEncryptionService`. |
| 5 | **L-5** (soft-skip tests) | Everything after this needs trustworthy tests. |
| 6 | **H-5, H-6** (write-only governance flags) | Enforce or delete; do not ship misleading UI. |
| 7 | **H-3** (audit redaction list) | Trivial; same code path as H-2. |
| 8 | **M-1, M-2** (fail-open policy) | Small, contained. |
| 9 | **H-1** (permission model) | Design decision — implement or remove the UI. |
| 10 | **H-7, H-8** (vector/job tenant columns) | Schema change; bundle into the first Workspace migration. |
| 11 | **M-7** (chunk-level authz) | Required before Knowledge is shared across workspaces. |

**Not production-ready. Do not represent it as such.**

*No exploit was executed. All findings are from source reading and are cited to `file:line`.*