# Missing Backend Endpoints — Frontend UI Gap Analysis

> **Author:** Frontend / UI Engineering
> **Status:** GATE-A items remain specification only. Every GATE-B item in §3.1 and §3.3 has since
> shipped end to end (real backend + real UI, live-verified), see the "CLOSED" notes inline.
> **Date:** 2026-09-28 (updated 2026-09-29)
> **Audience:** Backend team building the next API increment.
> **Related:** [API.md](./API.md) (endpoints that *do* exist), [../../ROADMAP.md](../../ROADMAP.md)
> (target architecture/roadmap), [../architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md)
> (recreated 2026-09-30 — the original root-level `ARCHITECTURE.md` this note used to point to was
> deleted; this is a new file at a new path, not a restoration of the old one)

---

## 1. Why this document exists

The UI/UX brief for the "Create Once. Connect Everywhere. Publish. Sell." positioning
calls for a Marketplace, a Billing area, website-widget theming, WhatsApp and Telegram
deployment wizards, MCP tool registration, and a self-service sign-up flow.

An audit of the API found **255 real actions across 25 controllers**. Roughly 70% of the
brief already ships. The remainder splits cleanly into two buckets:

| Bucket | Meaning | Action |
|---|---|---|
| **GATE-A** | Brief feature with **zero** backend surface. Any UI would be fiction. | Specification only, until the backend lands. |
| **GATE-B** | Brief feature **partially** wired; the entity/column exists but no DTO or command exposes it. | Small backend increment unlocks the UI. |

**GATE-A items still have no UI implementation, by deliberate choice.** The brief states
*"Do not hardcode financial transactions or claim payment success without backend
confirmation"* and *"Do not imply that a WhatsApp or Telegram connection is active before
the actual provider integration confirms it."* Building screens for GATE-A endpoints
would violate that instruction directly, so those screens stay deferred instead of faked.
**GATE-B items (§3.1, §3.3) have since been built and live-verified** — see the "CLOSED" notes
inline for what shipped and how it was verified.

Everything not listed as still-open below is already implemented and wired in
`src/R2WAI.Client/src/features/`.

---

## 2. GATE-A — no backend surface at all

### 2.1 Marketplace (Screen 11)

No `marketplace` token exists anywhere in the API, domain, or application layers. There is
no listing entity, no catalogue, no install/grant mechanism, no moderation queue, and no
seller model. The tenant boundary (`tenant_id` claim + EF global query filter) is the
mechanism a cross-tenant marketplace would have to deliberately step outside of, so this
is a significant design decision rather than a set of CRUD endpoints.

| # | Endpoint | Purpose | Notes |
|---|---|---|---|
| 1 | `GET /api/v1/marketplace/catalog` | Browse published listings | Filter by type, category, tag, price model, sort |
| 2 | `GET /api/v1/marketplace/listings/{id}` | Listing detail | Must redact publisher tenant identity unless disclosed |
| 3 | `POST /api/v1/marketplace/listings` | Publish a tenant asset as a listing | Source: assistant / knowledge base / integration / workflow |
| 4 | `PUT /api/v1/marketplace/listings/{id}` | Update own listing | |
| 5 | `DELETE /api/v1/marketplace/listings/{id}` | Unpublish | |
| 6 | `POST /api/v1/marketplace/listings/{id}/install` | Install into current tenant | Must copy definition, not share a row |
| 7 | `POST /api/v1/marketplace/listings/{id}/uninstall` | Remove installed copy | |
| 8 | `GET /api/v1/marketplace/installed` | What this tenant has installed | |
| 9 | `GET /api/v1/marketplace/listings/{id}/reviews` | Verified reviews | |
| 10 | `POST /api/v1/marketplace/listings/{id}/reviews` | Leave a review | **Only verified purchasers** may post |
| 11 | `POST /api/v1/marketplace/listings/{id}/report` | Abuse report | |
| 12 | `GET/PUT /api/v1/marketplace/seller/profile` | Seller onboarding | |
| 13 | `GET /api/v1/admin/marketplace/listings?status=pending` | Moderation queue | SuperAdmin |
| 14 | `POST /api/v1/admin/marketplace/listings/{id}/approve\|reject` | Moderation decision | SuperAdmin; reject requires a reason |

**Design question the backend must answer first:** does installing a listing create a
*copy* in the buyer tenant (correct, preserves isolation and lets the buyer diverge) or
subscribe the buyer to the seller's live entity (cheaper, leaks tenant boundaries)?
Recommendation: copy on install, record a `MarketplaceInstallation` row carrying the
source listing id so the seller can be informed of install counts and the buyer can
detect upstream updates.

### 2.2 Billing & Usage (Screen 15)

No plan, subscription, invoice, payment-method, or checkout entity exists. An earlier
internal points/wallet ledger (`MembersController`/`AdminMembersController`,
`/members/wallet`, `/members/points/convert`, `/members/withdrawals`) was removed
2026-09-30 as dead legacy surface (no UI, no tests, tied to the member/rewards feature —
not billing infrastructure). There is no plan catalogue, no payment provider, and nothing
in this codebase to build billing on top of yet.

| # | Endpoint | Purpose | Notes |
|---|---|---|---|
| 15 | `GET /api/v1/billing/plans` | Plan catalogue | Price, interval, feature flags, agent/seat/message limits |
| 16 | `GET /api/v1/billing/subscription` | Current subscription | Plan, status, period start/end, renewal date |
| 17 | `POST /api/v1/billing/checkout` | Start checkout | Returns a **provider-hosted** session URL. Never client-calculated |
| 18 | `PUT /api/v1/billing/subscription` | Change plan | Proration handled server-side |
| 19 | `DELETE /api/v1/billing/subscription` | Cancel | Effective-at-period-end vs immediate |
| 20 | `GET /api/v1/billing/invoices` | Invoice history | Paged |
| 21 | `GET /api/v1/billing/invoices/{id}` | Invoice detail | Line items, tax, proration credits |
| 22 | `GET /api/v1/billing/invoices/{id}/pdf` | Invoice PDF | Stream |
| 23 | `GET /api/v1/billing/payment-methods` | Saved payment methods | Metadata + brand only, **never** the PAN |
| 24 | `POST /api/v1/billing/payment-methods` | Add payment method | Provider-hosted tokenisation, no card data touches R2WAI |
| 25 | `DELETE /api/v1/billing/payment-methods/{id}` | Remove payment method | |
| 26 | `POST /api/v1/billing/webhooks/{provider}` | **Inbound payment callback** | Signature-verified. **Without this, no invoice can ever be marked paid.** Highest priority item in this document |
| 27 | `GET /api/v1/billing/usage` | Billable usage, current period | Distinct from the existing `AiUsagePolicy` daily cap |
| 28 | `GET /api/v1/billing/usage/estimate` | Pre-charge estimate | Must be labelled an **estimate** in the UI, never a charge |
| 29 | `POST /api/v1/billing/invoices/{id}/refund` | Refund | |
| 30 | `GET/PUT /api/v1/billing/tax` | Tax configuration | |
| 31 | `GET /api/v1/billing/dunning` | Failed-payment retry state | |
| 32 | `GET /api/v1/admin/billing/subscriptions` | Platform-wide subscription admin | SuperAdmin |
| 33 | `POST /api/v1/admin/withdrawals/{id}/payout` | Real payout provider call | The old status-only-flip withdrawal endpoint this would have replaced no longer exists (removed 2026-09-30); a real implementation starts fresh under billing, not as a follow-up to it |

**UI rule once this lands:** the client must render `usage/estimate` output with an
explicit "estimated, not finalised" label and must never optimistically mark an invoice
paid. Payment state is driven solely by the verified provider webhook (endpoint 26).

### 2.3 Telegram channel (Screen 9)

`ChatbotChannelType` is `Teams | Slack | WhatsApp | Sms`. Telegram is not a member. Note
that `ChatbotDetailPage.tsx:322` already renders a "Telegram" label in its channel list
that the type union does not contain — a dead string today.

| # | Endpoint | Purpose |
|---|---|---|
| 34 | Add `Telegram` to `ChatbotChannelType` | Enum change; requires a migration |
| 35 | `POST /api/v1/chatbots/{id}/channels/telegram` | Register a bot via BotFather token |
| 36 | `POST /api/v1/chatbots/telegram/{botId}/webhook` | Inbound update webhook, **verified against Telegram's secret token** |
| 37 | `GET /api/v1/chatbots/{id}/channels/telegram/status` | Live `getMe` probe, so the UI can show verified vs unverified |

### 2.4 Real messaging provider adapters (Screen 9)

`POST /api/v1/chatbots/{id}/channels/{channel}` currently validates the enum, then stores
the submitted payload **encrypted** (`ChatbotChannelConfiguration`). There is no outbound
send path and no inbound provider webhook routing for Teams, Slack, WhatsApp or SMS. A
"Test connection" button on the current backend could only ever report storage success, so
the UI does not offer one.

| # | Endpoint / work | Purpose |
|---|---|---|
| 38 | `POST /api/v1/chatbots/whatsapp/{phoneNumberId}/webhook` | Inbound, **HMAC-SHA256 signature verified** per Meta's scheme |
| 39 | `GET /api/v1/chatbots/whatsapp/{id}/status` | Verified status via Graph API |
| 40 | `GET /api/v1/chatbots/whatsapp/{id}/templates` | Approved message templates + status |
| 41 | `POST /api/v1/chatbots/whatsapp/{id}/test` | Send a real test message, report the provider result |
| 42 | `POST /api/v1/chatbots/teams/{...}/webhook`, `POST /api/v1/chatbots/slack/{...}/events` | Inbound for the other channels |
| 43 | Outbound sender behind `IChatChannelSender` | Replace storage-only with a real per-channel adapter |

### 2.5 ~~MCP server registration~~ (Screen 7, Tools & APIs) — **closed, stale entry**

Corrected 2026-10-01 — this no longer describes the repository. `McpConnectionsController`
(`api/v1/mcp-connections`, implementation plan Phase 3) is real, not a stub: tenant-owned CRUD
(`GET`/`GET {id}`/`POST`/`PUT {id}`/`DELETE {id}`), `POST {id}/toggle`, `POST {id}/test` (live
handshake via `McpClientAdapter`), `GET {id}/discover` + `POST {id}/commit` — the same
analyze-then-commit two-step `IntegrationsController` uses for OpenAPI, except committed
`ToolDefinition` rows land **inactive**: an MCP server can advertise arbitrary, server-controlled
tools, so nothing becomes agent-callable until an admin reviews and activates it. `EgressGuard`
validates the endpoint URL on create (same SSRF boundary as every other outbound-fetch path);
credentials go through `IEncryptionService.Encrypt` (functionally equivalent to this item's
original `IntegrationCredentialCodec` suggestion — encrypted at rest, never round-tripped).
`AI-RUNTIME.md` documents the one deliberate scope line: `AgentFrameworkRuntime`'s tool factory
excludes MCP tools outright (no HTTP-verb concept to classify as read-only against), so MCP tools
are Semantic-Kernel-only for now — a real, separate follow-on, not a gap in this section.

---

## 3. GATE-B — entity exists, API surface missing

These are cheap. Each unlocks a UI increment the brief asks for.

### 3.1 Website widget configuration (Screen 8) — highest UI value

**#50/#51 closed (2026-09-29).** `Chatbot.EmbedScript`/`WidgetSettings` were real columns
with a real domain setter (`Chatbot.UpdateWidget`) but zero callers — the widget deployment
page built the embed script and appearance config client-side only, on every visit, with
no server-side persistence across reloads or devices. `PUT /api/v1/chatbots/{id}/widget`
(`UpdateChatbotWidgetCommand`) now persists both; `GET`/list already return them via the two
new `ChatbotDto` fields (`embedScript`/`widgetSettings`), so no separate GET route was
needed. `WidgetDeploymentPage.tsx` re-seeds its form from the saved value instead of always
starting blank. #49/#52/#53/#54/#55/#56 below remain open.

**#53 closed too (2026-09-29), scoped down from "domain ownership verification" to "installation
detected".** A real DNS TXT-record ownership check proves the admin controls the domain, not that
the widget is actually live there, and would add a new outbound-fetch/SSRF surface for a status
light. Instead, `Chatbot.WidgetLastSeenAt`/`WidgetLastSeenOrigin` (new columns, migration
`AddChatbotWidgetLastSeen`) record the first time a real browser, from an allowed origin, loads the
widget and reaches the existing anonymous `GET .../public-info` bootstrap call — the same endpoint
the shipped bundle already calls on init, so no widget-bundle change was needed. Read back through
the existing `ChatbotDto` (`widgetLastSeenAt`/`widgetLastSeenOrigin`), no new GET route needed.
`WidgetDeploymentPage.tsx`'s "Embed verification" row and "Refresh" button use it now. **Live-verified
against the real Docker stack, not just unit tests**: rebuilt and restarted `r2wai-api`, created and
activated a chatbot, called `public-info` anonymously with a real `Origin` header, confirmed the
Postgres row updated (`2026-09-29 09:43:27+00 | https://www.livecheck-example.com`) and that
`GetById`/the widget PUT both reflect it without clobbering each other.

**#54 closed too (2026-09-29).** No `Conversation`/`Message` rows are ever persisted for the
anonymous chatbot paths (`Chat`/`StreamChat`/`Webhook` reply and return, nothing is saved
per-turn), so there was no existing table to `COUNT(*)` from. Added `Chatbot.TotalMessagesServed`
(new column, migration `AddChatbotTotalMessagesServed`), atomically incremented via the same
relational-only `ExecuteUpdateAsync` pattern as #53's last-seen tracking, at all three real reply
paths. Combined with the tenant-wide `AiUsage` policy status (`IAiUsagePolicyService` gained
`GetStatusAsync`, a read-only counterpart to the existing `IsUnderCapAsync`/`RecordRequestAsync`
pair) behind `GET /api/v1/chatbots/{id}/usage` — one call answers both "how much has this widget
been used" and "how close is the organisation to its shared cap". **Live-verified against the real
Docker stack**: created and activated a chatbot, confirmed usage started at
`{"totalMessagesServed":0,"tenantDailyCap":null,"tenantDailyUsed":0}`, sent one real anonymous chat
message through Ollama (~60s, matches this environment's known local-LLM latency), confirmed both
the API response and a direct Postgres query agreed on `TotalMessagesServed = 1`, and that
`tenantDailyUsed` also incremented to 1 from the same call. `WidgetDeploymentPage.tsx` shows this
in a new "Usage" card.

**#55 closed too (2026-09-29).** `Chatbot.PositiveFeedbackCount`/`NegativeFeedbackCount` (new
columns, migration `AddChatbotFeedbackCounts`) are atomically incremented by the new
`POST /api/v1/chatbots/{id}/feedback` (anonymous, same origin/status gate as `Chat`/`StreamChat`,
`{"rating":"up"|"down"}`, 400 on anything else). Not linked to a specific message — no `Message`
rows exist for these anonymous paths (see #54's note) — so this is a lifetime tally, not
per-conversation feedback history. The shipped widget bundle (`src/R2WAI.Widget`) now shows a
👍/👎 row under each completed reply (one-shot — disables after voting, since the counter is a
one-way increment, not a settable value); surfaced in `GET .../usage` and the admin's Usage card.
**Live-verified against the real Docker stack**: rebuilt and restarted both `r2wai-api` and
`r2wai-studio`, confirmed the served `/widget/widget.js` actually contains the new code (not a
stale cached layer), then exercised the full flow — invalid rating correctly 400s, 2×up + 1×down
correctly returns `{"positiveFeedbackCount":2,"negativeFeedbackCount":1}` from both the API and a
direct Postgres query, and confirmed feedback calls do *not* count against the tenant's AiUsage
daily cap (a feedback vote isn't an AI request).

**#49 assessed and deliberately not built.** `WidgetPreview.tsx`'s own doc comment already
explains why it simulates replies locally: the real chat endpoints are origin-checked against the
chatbot's `AllowedOrigins`, which will never include the Studio's own origin, so a "real" bootstrap
payload could not make the preview call the live endpoint anyway — the gap was never "one payload
vs. two", it's a CORS/origin boundary a bootstrap endpoint doesn't change. No remaining value found.

What the deployed widget still needs:

| # | Endpoint | Purpose | UI it unblocks |
|---|---|---|---|
| 52 | `GET/POST/DELETE /api/v1/chatbots/{id}/embed/domains[/{domain}]` | Manage `AllowedOrigins` as first-class resources | Low value — the current plain-textarea editor is already functionally equivalent (add/remove chips, one PUT) |

**#56 closed too (2026-09-29).** `POST /api/v1/chatbots/{id}/messages/attachment` — anonymous,
same origin/status gate as `Chat`/`StreamChat`/`SubmitFeedback`, but deliberately much tighter than
`DocumentsController`'s authenticated upload it's modeled on: **5 MB** (not 50 MB) and
**images/PDF/plain-text only** (not Office formats — no macro risk, no legitimate support-chat use
case), because this route is reachable by anyone who can load the widget, not a tenant admin behind
`CanManageDocuments`. The stored filename is always server-generated (`Guid`-based), never derived
from the client-supplied name, so path traversal/collision is structurally not possible regardless
of the filename validation above it. No Message entity exists for the anonymous chat paths (see
#54's note), so this is "attach then reference": the response returns the file's URL, and the widget
bundle inserts it as plain text into the visitor's next message rather than attaching to a specific
turn. The widget renders all messages via `textContent`, never `innerHTML` (its own existing XSS
defense) — a clickable attachment link was deliberately out of scope rather than adding HTML
rendering to reach it. **Known, accepted limitation, not silently dropped**: no rate limit on this
route beyond the origin allowlist, the same boundary `ChatbotEmbedDialog` already discloses to
admins for the chat endpoints themselves — not a new gap introduced here.

**Live-verified against the real Docker stack**: rebuilt and restarted both `r2wai-api` and
`r2wai-studio`. Confirmed all four cases for real — nonexistent chatbot → 404, a `.docx` → 400
("not supported"), a 6 MB file → 400 ("exceeds the 5 MB limit"), a valid `.txt` → 200 with a real
storage URL — then found the file on the container's actual disk
(`/app/App_Data/Storage/chatbot-attachments/{chatbotId}/{random}.txt`) with byte-for-byte matching
content. Confirmed the served `widget.js` contains the new attach-button code. Cleaned up the test
chatbot afterward.

`GET /{id}/public-info` is anonymous and currently returns only
`{ name, welcomeMessage, voiceEnabled, suggestedQuestions }`. Any branding or theming added
here becomes **publicly readable by anyone who knows the chatbot id** — decide deliberately
what belongs in the anonymous payload versus a token-gated one.

### 3.2 Knowledge crawling and sync (Screen 5)

URL sources are fetched and chunked **once, inline** by `KnowledgeBaseService`. There is no
sitemap/robots crawl, no schedule, and no sync-job entity. `KnowledgeBaseSourceDto` does
expose `Status`, `ChunkCount`, `IndexedAt` and `Error`, but only nested inside
`GET /knowledgebases/{id}` — there is no standalone source or indexing-status endpoint, so a
tenant-wide "Indexing Activity" view cannot be built yet.

| # | Endpoint | Purpose |
|---|---|---|
| 57 | ~~`GET /api/v1/knowledgebases/sources?status=`~~ **Closed 2026-09-30.** `GetKnowledgeBaseSourcesQuery` + `KnowledgeBasesController.GetSources` — flat, paged, `status`-filterable, tenant-wide. `KnowledgeBaseSourceDto` gained `KnowledgeBaseId`/`KnowledgeBaseName` (additive, existing nested usage under `GET /knowledgebases/{id}` unaffected) so a flat row can still identify its parent KB. `KnowledgeBaseSource` has no `TenantId` column, so this can't rely on the ambient EF filter like a normal tenant-scoped query — tenant isolation is enforced explicitly through the parent `KnowledgeBase` (`s.KnowledgeBase.TenantId == tenantId`), same pattern as every other no-TenantId entity from the P0-5 audit. Verified with a real two-tenant test proving tenant B's source never appears in tenant A's results, plus status-filter and KB-identity-passthrough cases (4 new tests, `GetKnowledgeBaseSourcesTests.cs`). No client UI built yet — no "Indexing Activity" screen exists to wire this into; that's a separate product/design decision, not bundled into this backend increment. |
| 58 | `POST /api/v1/knowledgebases/{id}/crawl` | Sitemap/robots crawl as a background job |
| 59 | `GET /api/v1/knowledgebases/{id}/sync-status` | Last/next sync, in-flight job |
| 60 | `POST /api/v1/knowledgebases/sources/{sourceId}/reindex` | Per-source retry |
| 61 | `GET/POST/PUT/DELETE /api/v1/knowledgebases/sync-jobs` | Sync schedule CRUD |
| 62 | `POST /api/v1/knowledgebases/{id}/sources` accepting a sitemap URL | Sitemap as a source type |

The existing `BackgroundJobPayloads` + `IndexDocumentJobHandler` background-job
infrastructure is the right host for 58/60/61; this is queueing work, not new machinery.

### 3.3 Assistant versioning and cloning (Screen 3) — CLOSED (2026-09-29)

Mirrors the proven Workflows/KnowledgeBases/Capabilities version+rollback pattern exactly: new
`AssistantVersion` entity (migration `AddAssistantVersions`), `GET/POST /api/v1/assistants/{id}/versions`,
`POST /api/v1/assistants/{id}/versions/{versionId}/rollback`. `POST /api/v1/assistants/{id}/clone`
(#65, the brief's "Duplicate" card action) always creates a fresh, unpublished draft — copies the
source's full config (including a `null` Tools value verbatim, i.e. the legacy "all tools" behavior,
rather than narrowing it) but never its `PublishStatus`/`IsActive`/`PublishedVersion`, so duplicating
a live published assistant can never silently publish a second one. `Type` is captured in the
snapshot for record-keeping only — `AssistantDefinition` has no setter for it, so rollback does not
attempt to restore it (nothing can have diverged).

**"Duplicate" is wired into the client** (`AssistantsLibraryPage.tsx` card actions). Version
history/rollback have **no client UI**, matching this codebase's own existing precedent — the
identical KnowledgeBase/Workflow/Capability endpoints also have no UI anywhere in this client today.

**Live-verified against the real Docker stack**: created an assistant, snapshotted v1, updated its
name/system prompt, cloned it (confirmed the clone copied the *current* config but started
`"publishStatus":"Draft","isActive":false,"publishedVersion":0`), rolled the original back to v1
(confirmed both the API response and a direct Postgres query show the name/prompt reverted and
`AssistantVersions` holds exactly 2 rows: v1 unpublished, v2 "Rolled back to v1" published).
Confirmed the rebuilt Studio bundle actually contains the new "Duplicate" control, not a stale layer.

### 3.4 Tenant self-service and sessions (Screens 1, 14)

**#66 already satisfied under a different route, not actually missing** — `SettingsPage.tsx`'s
"General" tab already reads/writes the tenant's own name/slug/domain via `getSettings`/
`updateOrganizationDetails` against an existing `/settings`-family endpoint, not the
`/tenants/current` path this doc originally assumed. Confirmed by reading the live client code
rather than trusting the route name in isolation.

**#70 closed (2026-09-29).** `POST /api/v1/admin/users/{id}/mfa-reset` and `.../unlock` — real
gaps confirmed absent before this (no route anywhere disabled a user's MFA or cleared a login
lockout). Extracted the lockout cache key/state out of `AuthController` into
`Services/LoginLockoutCache.cs` so unlock clears *exactly* the cache entry the login flow itself
writes, not a second disconnected notion of "unlocked." Both actions audit-logged, gated by the
same `CanManageUsers` policy as every other action on `UserManagementController`. Wired into the
admin Users table (two new icon buttons). **Live-verified against the real Docker stack with real
state transitions, not just status codes**: enabled MFA on a real test user via Postgres, called
mfa-reset, confirmed via a direct query that `MfaEnabled` flipped `true → false` and `MfaSecret`
cleared; separately triggered a genuine lockout on another test user (5 real failed logins, 6th
attempt — even with the *correct* password — blocked with a real 429/"locked" response), called
unlock, then confirmed the exact same correct-password login that was just blocked now succeeded
with a real JWT. Both test users cleaned up afterward. This closed a real, pre-existing gap: this
controller had zero test coverage before today, and the fix's own first test run caught the
fail-closed tenant filter blocking a raw-DI-scope verification query — the same class of issue
documented repeatedly elsewhere in this codebase's test suite, fixed the same way
(`IgnoreQueryFilters()`).

**#67 closed (2026-09-29).** `GET/POST/PUT/DELETE /api/v1/admin/tenants`, SystemAdmin only.
"Delete" is suspend + soft-delete, not permanent data loss (this app's universal convention, see
`DeleteAssistantCommandHandler`). Building this surfaced a real, separate bug, found by live
verification, not by inspection: `TenantStatus` existed on the entity but was **checked nowhere in
the entire codebase** (confirmed by grep before touching anything) — a platform admin "suspending"
a tenant had zero actual effect on whether that tenant's users could still log in. Fixed by adding
real enforcement to `AuthController.Login`, `.Refresh`, and `.ExchangeEntraIdToken` (SSO doesn't
bypass it either, matching the existing MFA-floor precedent in the same file). Self-lockout guards
prevent a SystemAdmin from suspending or deleting their own current tenant, both server-side and
in the client UI (the status field disables itself with an explanatory note). New "Organizations"
tab on the Users & Roles page, visible to SuperAdmin only.

Live verification itself caught a **second** real, previously-latent bug:
`ApplicationDbContext.OnModelCreating` unconditionally `continue`d past `Tenant` in its reflective
filter loop (originally to avoid a circular tenant-filter-on-itself), which also skipped the
*legitimate*, non-circular soft-delete filter `Tenant` should have gotten — `HasTenantIdProperty`
already returns `false` for `Tenant` (`Id`, not `TenantId` — it IS the tenant), so no special case
was actually needed. A deleted tenant kept appearing in `GetTenantsQuery` until this was fixed,
because `Tenant.SoftDelete()` had never been called by anything in the whole codebase before this
command existed. Fixed by removing the unconditional skip so `Tenant` falls through to the same
soft-delete-only branch `AccessRequest` (also `TenantId`-less) already used correctly.

**Live-verified against the real Docker stack, both bugs, with real state transitions**: created a
throwaway tenant, logged in successfully as a user in it, suspended it via the real admin endpoint,
confirmed that same correct-password login now returns 403, confirmed an already-issued refresh
token is rejected and revoked, confirmed the self-suspend guard rejects touching the SystemAdmin's
own tenant (400 with a clear message) — then, after the soft-delete fix, created and deleted a
second tenant and confirmed it no longer appears in the admin list while its DB row survives
(`Status=Suspended, IsDeleted=true`), not silently lost. Full regression pass after the DbContext
change: Domain 194/194, Application 108/108, Infrastructure 262/262, all unchanged.

**#68/#69 remain open** — multi-device session listing/revocation. This app's current auth model
stores a *single* `RefreshTokenHash` per `User` row (confirmed by reading `User.cs`), not a
collection — logging in on a second device overwrites the first device's token rather than adding
to a session list. A real "active sessions" UI implying multiple concurrent per-device sessions
would need a new `UserSession`-shaped entity first; that's a genuine architecture change, not a
quick increment, so it's correctly left open rather than built to look richer than the data model
actually supports.

**#71 (persisted onboarding-state) is deliberately not needed, confirmed by reading the client.**
`OnboardingChecklist.tsx`'s own doc comment already explains why: it derives every checklist item
from live tenant data instead of a persisted "completed"/"skipped" flag, on purpose — *"a checklist
that reports real state is worth more than one that remembers a click."* Building #71 now would
regress a design that's already better than what the brief originally asked for; correctly left
unbuilt, not silently missed.

| # | Endpoint | Purpose |
|---|---|---|
| 68 | `GET /api/v1/sessions` | List active sessions (device, IP, last seen) — needs a `UserSession` entity first, see above |
| 69 | `DELETE /api/v1/sessions/{id}` · `POST /api/v1/sessions/revoke-all` | Revoke — same prerequisite as #68 |

### 3.5 Integration catalogue and OAuth (Screens 3, 6)

**#72 closed (2026-09-29).** `GET /api/v1/integrations/catalog` returns a curated, static list of
9 well-known connectors (Slack, Teams, Google Sheets, Notion, HubSpot, Salesforce, GitHub, plus
generic webhook/REST entries) spanning the brief's Messaging/Productivity/CRM/Developer Tools/REST
API categories. Every entry deliberately suggests `Http` — the only `ToolType` `DynamicToolFunctionFactory`
actually makes callable (Database/Script/Custom exist as enum values with no execution path, confirmed
by `IntegrationConnectorTests`'s own 422 "not executable yet" case). The client's new "Catalog" tab
renders these as cards; "Connect" opens the *same real* create-integration dialog, pre-filled with
the entry's suggested name/endpoint/auth type — never a fake OAuth success. **Live-verified**: 401
without auth, 200 with the full 9-entry list with auth, then proved the prefill data is genuinely
usable by creating a real integration from the Slack entry's exact suggested values end to end
(confirmed readable afterward, secret correctly redacted on read-back, cleaned up).

**#73/#74 (real OAuth 2.0/PKCE) stay open, deliberately** — no per-provider app registration
(client id/secret) exists for any catalogued provider, so a connect flow would have nothing to
authenticate against; building the flow without one would mean shipping a button that always fails
or, worse, appears to succeed without a real token. Needs provider app registrations decided and
provisioned first, not more coding.

| # | Endpoint | Purpose |
|---|---|---|
| 73 | `POST /api/v1/integrations/{providerId}/connect` | Start an OAuth 2.0 / PKCE authorisation |
| 74 | `GET /api/v1/integrations/oauth/callback` | Callback; code exchange stays server-side |
| 75 | `GET /api/v1/integrations/{id}/status` | Live connection health, replacing the storage-only flag |

### 3.6 Monitoring gaps (Screen 12)

`OperationsController` has good coverage, but three things the brief asks for have no
dedicated endpoint: knowledge **retrieval traces**, a structured **per-request failure
log** (`GET /operations/errors` reads server log *files*, not request records), and a
**tool-execution log** (tool events currently surface only live over SSE in the playground
inspector, and per-step in the runs inspector).

| # | Endpoint | Purpose |
|---|---|---|
| 76 | `GET /api/v1/operations/retrieval-traces` | Per-query chunk hits, scores, latency |
| 77 | ~~`GET /api/v1/operations/tool-executions`~~ **Partially closed 2026-09-30, deliberately scoped down.** Investigated first: `AiFunctionAuditFilter` already timed every call with a `Stopwatch` and already had a raw-arguments string (`context.Arguments` dump) ready to hand off — but both were only ever handed to the ephemeral, per-request `IChatTraceCollector` ("Test Studio"), never persisted. `WriteExecutionAuditAsync`'s `AuditLog.Metadata` now includes `durationMs` alongside the existing `status`/`function`/`error` — durationMs is a plain number, safe to persist unconditionally, so no new route was needed: `GET /operations/audit-logs?entityType=ToolDefinition&action=Execute` already exposes it, filterable by tool/user/date, already RBAC-gated. **Deliberately did not persist the raw arguments** — that string is whatever the AI passed to any tool, completely unredacted (could be a customer's PII, an API payload, anything), and durably logging it into a broadly-queryable audit table needs a real redaction design decision first (which fields, which tools, opt-in per `ToolDefinition`?), not a quick wire-up — flagging this rather than either skipping it silently or shipping something unsafe. Verified with 2 new tests in `ToolGovernanceFilterTests` (success and failure paths) reading the real `AuditLog` row back through the real filter + real Kernel, not mocked. |
| 78 | `GET /api/v1/operations/failed-requests` | Structured request-failure records, not log lines |

### 3.7 Other smaller gaps

| # | Endpoint | Purpose |
|---|---|---|
| 79 | `GET/POST/PUT/DELETE /api/v1/integrations/{id}/webhooks` · `/{id}/test` | Per-webhook delivery log and test send |
| 80 | ~~`GET /api/v1/notifications/unread-count`~~ **Closed 2026-09-30** — `NotificationsController.GetUnreadCount`. The count itself already existed inside `GET /notifications`'s response; this is a lightweight dedicated route so a polling navbar badge doesn't have to load and paginate a full page just for one number. Verified through the real endpoint, not just unit logic: seeded 2 unread + 1 read for the caller plus 1 unread for another user in the same tenant, confirmed the delta is exactly 2 (tenant-wide leakage would have shown 3). `GET/PUT /preferences` remains open — no preference entity/columns exist yet, and "what preferences" is a real design decision, not a coding task. |
| 81 | ~~`GET/PUT/DELETE /api/v1/assistants/prompt-templates/{type}`~~ **Closed 2026-09-30.** GET already existed; `IPromptTemplateService.SetTemplateAsync` (versioned edit, supersede-on-write) also already existed with no route calling it. Added `PUT` (wires the existing method) and a new `ResetTemplateAsync`/`DELETE` (supersedes the active override with no replacement, falling back to `SystemPromptTemplates`' default — a no-op, not an error, when there's nothing to reset). Unknown `{type}` segment → 400, not a 500 or silent no-op. Verified: 5 new `PromptTemplateServiceTests` (reset semantics, no-op case, per-tenant isolation) plus a real HTTP-level round trip in `AssistantFlowTests` (PUT → GET reflects override → DELETE → GET shows the static default again) and 401/400 cases. |
| 82 | ~~`GET/PUT /api/v1/workflows/templates`~~ **Closed 2026-09-30.** The 5 templates were hardcoded anonymous objects in `WorkflowsController.GetTemplates`, unreachable for editing. Moved them unchanged into `WorkflowTemplateDefaults` (Prompt Management's sibling — mirrors `SystemPromptTemplates`); new `WorkflowTemplateOverride` entity (migration `AddWorkflowTemplateOverrides`, one row per tenant+templateId, unique index) + `IWorkflowTemplateService` let a tenant `PUT` an edit that's merged over the static defaults on `GET`, in place (not versioned — nothing reads workflow-template edit history the way `PromptTemplate` does). Unknown `{id}` on `PUT` → 404 via `NotFoundException`, not a silently-created row for a template that doesn't exist. Verified: 5 service-level tests (default passthrough, override/merge, in-place update not a second row, per-tenant isolation, unknown-id 404) plus a real HTTP round trip in `WorkflowsControllerTests` (PUT → GET reflects the edit, sibling templates untouched, 404/401 cases) — 7 new tests there, full Workflow-tagged API slice (57 tests) unaffected. Migration applied to the dev Postgres. |
| 83 | `GET /api/v1/locales` · `/{code}/strings` | No i18n surface; all strings are hardcoded English |
| 84 | `GET /api/v1/chat/suggested-actions` rework | Currently returns 4 constants and **ignores its own `conversationId` input** |

---

## 4. Backend defects surfaced by this audit

Not strictly "missing endpoints", but they block correct UI and should be scheduled.

1. ~~**`CanManageDocuments` and `CanManageWorkflows` exclude `SystemAdmin`.**~~ **Fixed
   2026-09-29** — both now `RequireRole("Admin", "SystemAdmin")` (`Program.cs`), matching the
   adjacent `CanManageUsers`/`AdminOnly` policies and the comment above them stating all three
   should "collapse to what they always effectively granted... via the nav mapping" (the
   `SuperAdmin` persona's nav already links `/knowledge` and `/automations`, which route to
   these controllers). Live-verified: created a throwaway user holding **only** `SystemAdmin`
   (no `Admin`) — `GET /documents` and `GET /workflows` were 403 before the fix, both 200 after.
   Probe user deleted afterward.
2. ~~**`POST /workflows/draft` returns HTTP 200 with an empty draft on failure**~~ **Fixed
   2026-09-29** — worse than described: the client didn't treat an empty draft as failure at
   all, so it silently created a real, near-blank workflow (0 steps, no trigger, a
   truncated-description fallback name) and told the user "Automation drafted" — not just a
   blank wizard step. `WorkflowsController.DraftFromDescription`'s catch block now returns
   `502` with `{error: "..."}` instead of `Ok(new WorkflowDraft(null, null, [], []))`; the
   client's `fetchJson` already throws on non-2xx and `CreateAutomationDialog`'s existing catch
   already shows an honest "Failed to draft automation" message — both were dead code for this
   path until now. Live-verified for real, not simulated: a real request hit the exact
   pre-existing Ollama-timeout condition from this session's earlier findings
   (`ModelGatewayDefaults.OllamaNetworkTimeout`, unrelated to this fix) and correctly surfaced
   as `502` with the new message instead of a fake success. The happy `try` path (unchanged by
   this fix) wasn't separately re-proven live — this local model reliably free-runs past the
   150s timeout on this unbounded-`maxTokens` call, same as every other slow-model finding this
   session; not fixed here, out of scope for a catch-block error-status fix.
3. ~~**`ChatbotDetailPage.tsx:322` renders a "Telegram" channel label** that is not in
   `CHATBOT_CHANNEL_TYPES`~~ — **already fixed, stale entry.** Verified 2026-09-29: no selectable
   Telegram control exists anywhere in the client. `roleNav.ts:77` documents it as "deliberately
   absent everywhere: it has no enum member, adapter or webhook", backed by
   `roleNav.test.ts`'s `offers no Telegram entry anywhere`. The one remaining "Telegram" string
   (`ChatbotDetailPage.tsx:343`) is inert tooltip prose listing third-party platforms a customer's
   *own* custom integration could target via the generic webhook channel — not a control.
4. ~~**Swagger declares `Bearer` and `ApiKey` as jointly required** on every operation~~ **Fixed
   2026-09-30.** `Program.cs` called `AddSecurityRequirement` once with both schemes inside a single
   `OpenApiSecurityRequirement` — per the OpenAPI 3 spec, entries within one requirement object are
   ANDed (both required), while separate objects in the document's `security` array are ORed
   (either satisfies it). Split into two separate `AddSecurityRequirement` calls, one scheme each, so
   generated clients and the Swagger UI now correctly show Bearer and ApiKey as alternatives. Verified
   by build and against the spec's documented semantics, not a live Swagger UI run — the running
   `r2wai-api` container is `ASPNETCORE_ENVIRONMENT=Production`, which doesn't serve `/swagger` at
   all, and standing up a separate Development-mode instance just to screenshot this cosmetic fix
   wasn't worth the disruption.
5. ~~**`schema.d.ts` is a stub**~~ **Closed 2026-09-29 — deleted, not adopted.** Confirmed zero
   real call sites (`grep`: only `client.ts` itself imported `openapi-fetch`/the schema; one
   stale comment in `DeveloperPage.tsx` pointed at it descriptively, now repointed at the real
   client, `fetchJson.ts`). Adopting the typed client instead would mean migrating every one of
   this app's API call sites off the working `fetchJson`/`postJson` pattern for no functional
   gain — disproportionate for dead scaffolding nothing depends on. Deleted `lib/api/client.ts`,
   `lib/api/schema.d.ts`, the `generate:api` script, and the now-unused `openapi-fetch`/
   `openapi-typescript` package.json entries (`npm install` dropped 27 transitive packages).
   Verified: `tsc -b` clean, Vitest 114/114 (18 files) green, `oxlint` shows only pre-existing
   warnings unrelated to this change.
6. ~~**`GET /assistants` has no upper bound on `pageSize`.**~~ **Fixed 2026-09-29** —
   `AssistantsController.GetList` now clamps `page = Math.Max(1, page)` and
   `pageSize = Math.Clamp(pageSize, 1, 100)` before building `GetAssistantsQuery`, matching
   `ApiKeysController`/`DocumentsController`/`ApprovalsController`/`UserManagementController`/
   `AdminController`. Live-verified against the running API: `pageSize=100000` → `100` in the
   response, `page=-5` → `1`.
7. ~~**`GET /assistants` cannot filter or sort by publish status.**~~ **Fixed 2026-09-29** —
   `GetAssistantsQuery` gained `PublishStatus`/`SortBy` (`recent` default, `name`, `usage`,
   `least-used` — mirrors the client's existing `ASSISTANT_SORTS` keys exactly), and the
   response (`AssistantsPagedResult : PagedResult<AssistantDto>`) gained an additive
   `StatusCounts` field computed over the same search/applicationId scope as the items but
   *not* narrowed by the status filter itself, so switching status tabs never has to guess at
   the other tabs' counts. `AssistantsLibraryPage.tsx` now sends both as real query params
   instead of re-filtering/re-sorting the loaded page in the browser; the now-dead
   `applyAssistantView`/`countAssistantStatuses` functions and their 15 tests were deleted
   (`assistantView.ts` keeps only the shared constants/types both the query and the filter UI
   still need). Live-verified against the real API: `publishStatus=Published` → only Published
   items, `sortBy=name` → alphabetical, no filters → `StatusCounts` sums to `totalCount` (7=7),
   and `publishStatus=Draft&sortBy=usage` combined correctly narrows to the one Draft
   assistant. Client: `tsc -b` clean, Vitest 99/99 (down from 114 — the 15 deleted tests, no
   unexpected drop). UI not separately driven in a browser this pass (no browser tool
   available in this environment) — verified via typecheck, unit tests, and live API calls
   exercising the exact query shape the page now sends, not a manual click-through.

---

## 5. Recommended backend increment order

Ordered by UI value delivered per unit of backend effort.

| Priority | Item | Why |
|---|---|---|
| ~~1~~ | ~~3.1 widget config (49–53)~~ | **Done 2026-09-29** — #50/#51/#53/#54/#55/#56 shipped and live-verified; #49 assessed and correctly skipped (no real gap); #52 remains, low value |
| 1 | **2.4 + 2.3 provider adapters, Telegram (33–43)** | Without these, no messaging deployment screen can honestly say "connected" — now the top open item |
| ~~2~~ | ~~3.3 assistant versions/clone (63–65)~~ | **Done 2026-09-29** — shipped and live-verified; Duplicate has a client UI, version history/rollback stay backend-only matching this codebase's existing precedent for the identical KnowledgeBase/Workflow/Capability endpoints |
| 2 | **2.2 endpoint 26 payment webhook** | Nothing else in billing works without it; the rest of billing is then routine CRUD |
| ~~3~~ | ~~3.5 integration catalogue listing (72)~~ | **Done 2026-09-29** — live-verified; 73/74/75 remain, blocked on real per-provider OAuth app registrations, not coding |
| 3 | **3.2 knowledge crawl/sync (57–62)** | Reuses existing background-job infrastructure |
| 5 | **2.1 marketplace (1–14)** | Largest design decision (copy vs subscribe on install); needs a spec review first |
| ~~6~~ | ~~3.4 tenant self-service (66, 67, 70)~~ | **Done** — #66 was already satisfied under a different route; #70 (admin MFA reset/unlock) and #67 (platform tenant CRUD + real TenantStatus enforcement, which surfaced and fixed two latent bugs) both shipped and live-verified 2026-09-29; #71 correctly not needed (see inline note); #68/69 remain, blocked on a real architecture change (multi-session data model), not a quick increment |
