# Threat Model

> **Source:** ported from `docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md` §32–33 and §21–22 (2026-09-20
> snapshot), with a **Δ** line added per closed item from this session's verified work. Unmarked
> rows have not been re-verified since the snapshot.

## Classical

| Area | 2026-09-20 status | Δ |
|---|---|---|
| Authentication / JWT / refresh / MFA | with defects | Login-lockout/MFA-reset admin actions added; not otherwise re-verified. |
| API keys | role minting, per-request full scan, static keys without tenant | Role clamp added; static-key tenant requirement not re-verified. |
| Entra SSO | exchange only | **Closed for tenant filter:** Entra login (anonymous, no ambient tenant claim) now correctly finds already-provisioned users under the fail-closed filter (`.IgnoreQueryFilters()`), same fix shape as `AuthController.Refresh`. |
| CORS | prod allow-list; `PublicChatbotCors` open by design with per-chatbot origin check | Unchanged. |
| XSS | widget uses `textContent` (safe); SPA has no CSP | Unchanged. |
| **SSRF** | ❌ no guard on real dispatch, only the Test button | **Closed.** `EgressGuard` (IPv4 private ranges + full IPv6 — loopback/link-local/ULA/site-local, bracket-stripped) applied to `DynamicToolExecutor` (real AI-invoked + approval-resumed calls), `IntegrationsController.Test`, the workflow "API Call" step's node-creation check, and `OpenApiImportService`'s spec-fetch (a third, previously-uncorrected duplicate of the same check, fixed 2026-09-30). |
| SQL injection | EF parameterized; one internal-int interpolation | Unchanged. |
| Webhooks | chatbot webhook key hashed + constant-time; workflow webhook not reviewed | Unchanged. |
| **SignalR** | ❌ `StatusHub`/`ChatHub` authorization gap under the (then fail-open) tenant filter | **Closed** — both hubs now correctly authorize under the fail-closed filter via explicit claims-based checks (hub invocations don't reliably populate `HttpContext`, confirmed a real ASP.NET Core/WebSocket limitation, not codebase-specific). |
| Rate limiting | per client key, Redis-backed with fallback; no per-tool/per-tenant model concurrency | Unchanged. |
| Tenant escape | see durable-execution/tenant-isolation section below | **Substantially closed** — see Tenant Isolation section. |
| Logging | tool arguments logged at Information level | Unchanged — still logs raw arguments; see AI-specific data-exfiltration note below. |

## AI-specific

| Threat | 2026-09-20 status | Δ |
|---|---|---|
| Prompt injection / indirect | no untrusted-content policy or delimiting | Unchanged — real, open gap. |
| Tool injection / poisoning | discovery imports spec summaries as tool descriptions verbatim | Unchanged. Discovered tools land as inactive drafts requiring admin review before activation (already true), which is the primary mitigation; description-content sanitization itself not added. |
| RAG poisoning | any uploader; no provenance/trust tier | Unchanged. |
| Data exfiltration | unbounded tool/RAG output; SSRF→RAG path | SSRF path closed (see above). Output-size bounding not addressed. |
| Excessive agency | default "all tools" when an assistant has none selected | **Closed for new assistants** — `AssistantDefinition.DenyAllToolsByDefault()` now runs on creation (`Tools = "[]"`, not null). Existing null-Tools assistants keep the old "all tools" behavior unchanged, by design (no retroactive narrowing). |
| Privilege escalation | API-key roles; `submit_approval_request` | Not re-verified. |
| Cross-tenant retrieval | vector design had no tenant column | **Verified safe by tracing the real code**, not assumed: `vector_embeddings` has no `TenantId` column, but isolation is via `collection_name` (a `Guid`-derived, non-guessable string), and both real callers (`SearchKnowledgeBaseAsync`/`RemoveSourceAsync`) resolve their parent `KnowledgeBase` through the tenant-filtered `KnowledgeBases` DbSet first — a cross-tenant `knowledgeBaseId` 404s before the vector store is ever touched. |
| Unsafe tool arguments | no schema; raw JSON body | Unchanged. |
| Unauthorized MCP | n/a — not present | Still not present; MCP SDK verified available, nothing built (see Feature Matrix). |

**Data-protection note (unchanged):** the platform stores Aadhaar-linked identifiers and
detects/redacts Indian PII in chat — legal/compliance review remains warranted before any
government deployment. The Aadhaar hash itself moved from unsalted SHA-256 (brute-forceable
offline given Aadhaar's low entropy) to a keyed HMAC — closed for new registrations only.

## Secrets

| Secret | 2026-09-20 status | Δ |
|---|---|---|
| Model API keys | AES-GCM or config/env | Unchanged. |
| Enterprise API credentials (`ApplicationApi`) | AES-GCM | Unchanged. |
| **Direct-endpoint credentials (`ToolDefinition.Configuration`)** | ❌ plaintext | **Closed.** `IntegrationCredentialCodec` — AES-256-GCM, redacted on every read DTO (including the "Edit Integration" dialog, which previously pre-filled the live plaintext secret — a live cross-tenant-readable-by-any-admin leak, worse than the original finding described), legacy-plaintext decrypt fallback so already-stored rows keep working until re-saved. |
| OAuth | static pasted tokens; no refresh/rotation | Unchanged. |
| Chatbot channel payload | AES-GCM | Unchanged. |
| DB / MinIO / JWT | env/config; prod placeholder guard | Unchanged. |
| Encryption key | env-only outside Development; single key, no rotation | Unchanged — still a real, open risk (losing the key makes Aadhaar/API-credential/model-key/channel-payload data unrecoverable). |

**Still recommended, not built:** an `ISecretStore` abstraction (`Resolve(SecretRef)`) so the
database stores only a reference + key version, with envelope encryption and a rotation job.

## Durable-execution / crash-duplication scenario (the concrete idempotency trigger)

**Scenario:** an external tool call succeeds, the process crashes before recording that success,
the workflow resumes after restart, and the same external call executes a second time.

**2026-09-20 finding:** could happen — `WorkflowStepExecution(InstanceId, StepIndex)` has no
uniqueness constraint and no external-effect record; no `TenantId + WorkflowRunId + WorkflowStepId
+ IdempotencyKey` concept exists anywhere in the tool-call or workflow-step path.

**Δ:** still open. This is the one concrete, evidence-based trigger case the current
implementation plan's Phase 5 (durable execution ledger) targets — deliberately not a speculative
universal idempotency framework, since no other concrete trigger case has been found. Related
sweeper-level races (schedule/delay/escalation double-firing across replicas) are already closed
via atomic claim (`ExecuteUpdateAsync`); what remains is lease-expiry reclaim for a
crashed-mid-processing job and the `ToolExecution` idempotency-key record itself.

## Tenant isolation

The single largest closed item since the 2026-09-20 snapshot. The global EF Core query filter's
fail-open default (`TenantId == null || match` — a null ambient tenant matched *every* tenant's
rows) was flipped to fail-closed (`TenantId != null && match`), then every path that legitimately
has no ambient tenant claim (anonymous chatbot widget endpoints, background sweepers, SignalR
hubs, Entra/refresh auth) was found and given an explicit, verified tenant check instead of relying
on the filter. Five real, live regressions were found and fixed in this process (anonymous widget
endpoints, `WorkflowBridge` delay-resume, approval-notification and document-indexing background
jobs, Entra SSO) — each confirmed via a real RED/GREEN test against a live Postgres, not assumed
fixed by inspection alone.
