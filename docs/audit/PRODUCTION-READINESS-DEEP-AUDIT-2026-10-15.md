# R2WAI — Production-Readiness Deep Audit (2026-10-15)

- **Question:** Is R2WAI production-ready, does it meet industry standard, and where are the gaps against Microsoft Copilot Studio (agents, multi-agent, workflows, "integrate any system") for an **on-premises** product?
- **Scope:** committed code at `22678c28d` on `main`. Backend (27 controllers, 306 endpoints, 103 commands), React client (40+ routes, 65 pages/dialogs), widget, Docker/K8s manifests, CI.
- **Method:** this is a fresh code-level check, not a re-read of older audits. Every finding below cites the file and line it was verified in. Inventory and cross-reference scripts were run over the whole tree (endpoint list, client→API call map, validator coverage, `IgnoreQueryFilters` scan, page state scan, dead-file scan).
- **Companion doc:** market positioning and competitor research is in [COMPETITIVE-GAP-ANALYSIS-2026-10-15.md](COMPETITIVE-GAP-ANALYSIS-2026-10-15.md). This document is the engineering depth-check behind it.

---

## 0. Verdict

**Not production-ready yet.** The platform is broad and mostly wired end to end (no broken UI→API calls were found, builds and type checks are clean, ~900 unit tests pass), but four classes of issue would fail a security review or a customer pilot:

1. **Authorization is enforced in the UI, not the API.** A plain `User` can create, edit and delete agents, integrations (with credentials), MCP servers, approval policies, tenant prompt templates and navigation through the API, although the UI hides those pages from them.
2. **The on-prem pitch is blocked by the SSRF guard.** Outbound calls to any private IP or `.corp`/`.local`/`.internal` host are always refused, with no allow-list. An on-prem customer cannot connect their internal ERP/HR system, which is the core value proposition. The same guard also misses DNS-rebinding and redirects.
3. **Several features look complete but do nothing.** Automation triggers "Schedule", "Payment Received", "Form Submitted", "Application Submitted/Updated" never fire. The "Action" step type only writes a log line, and it is what "Draft with AI" produces. Content-moderation settings are saved but never applied. Teams/Slack/WhatsApp/SMS channels exist only as enum values and placeholder screens.
4. **Deployment defaults are unsafe.** Postgres is published on the host in the *production* compose file, Redis has no password, the default compose runs `Production` with public demo accounts, and two High-severity vulnerable packages ship in the production projects while the CI gate only fails on "Critical".

**Against Copilot Studio:** R2WAI matches it on *breadth of concepts* (agents, knowledge, tools, multi-agent delegation, flows, approvals, publish, monitor) and beats it on the one thing Microsoft cannot offer: air-gapped deployment with local models. It trails clearly on **depth**: no topic/dialog designer, no human hand-off, no enterprise channels, no event-triggered (autonomous) agents, 9 catalogue connectors versus 1,400+, no ALM (export/import, environments) and no end-user authentication in the published chatbot. See §6.

### Scorecard

| Area | Status | Headline |
|---|---|---|
| Build / type safety | 🟢 Green | `dotnet build` 0 errors (360 warnings), `tsc -b` clean |
| Unit tests | 🟢 Green | Domain 205/205, Application 162/162, client 126/126; Infrastructure 405/406 (the 1 failure is a peer session's uncommitted work, see §8) |
| UI → API wiring | 🟢 Green | 180 client calls, 0 pointing at a missing route |
| Tenant isolation | 🟡 Amber | Filter is now fail-closed; 2 cross-tenant paths remain (§1.6) |
| API authorization (RBAC) | 🔴 Red | Function-level authorization missing on ~12 controllers (§1.1) |
| Outbound / SSRF | 🔴 Red | Blocks legitimate on-prem targets *and* is bypassable (§1.2) |
| Feature honesty | 🔴 Red | Triggers, Action step, moderation and channels are UI-only (§2) |
| CRUD completeness | 🟡 Amber | ~60 backend operations have no UI; key lifecycle actions missing (§3) |
| Forms / validation | 🟡 Amber | Server validation 83/103 commands; client schema validation in ~6/28 dialogs (§4) |
| Notifications | 🟡 Amber | Only 4 events notify anyone (§4.3) |
| Deployment / ops | 🔴 Red | DB exposed, Redis open, demo accounts in Production mode, vulnerable packages (§5) |
| Dead / duplicate code | 🟡 Amber | 5 overlapping "things an agent can call" concepts; several dead features (§7) |

---

## 1. Security (P0 / P1)

### 1.1 P0 — Broken function-level authorization (OWASP API5)

The global rule is only "authenticated" (`app.MapControllers().RequireAuthorization()` at [Program.cs:423](../../src/R2WAI.Api/Program.cs:423)). Role checks exist on Admin/Users/Models/Governance/Workflows/Documents, and the `IAuthorizedRequest` MediatR guard covers **23** requests (Admin + Tenants features only). Everything else accepts any signed-in user.

The client hides these pages from the `User` persona (`visibleTo: ADMIN_PERSONAS` in [roleNav.ts:75-116](../../src/R2WAI.Client/src/lib/nav/roleNav.ts:75)), so the UI and the API disagree on the role model.

| Endpoint (any `User` can call) | Evidence | Why it matters |
|---|---|---|
| `POST/PUT/DELETE /integrations`, `/test` | [IntegrationsController.cs:105](../../src/R2WAI.Api/Controllers/IntegrationsController.cs:105) | Creates `ToolDefinition` rows **with stored credentials**. The same rows via `/capabilities` require `SystemAdmin` ([CapabilitiesController.cs:19](../../src/R2WAI.Api/Controllers/CapabilitiesController.cs:19)), and a test asserts that ([RoleMatrixSecurityTests.cs:149](../../tests/R2WAI.Api.Tests/Security/RoleMatrixSecurityTests.cs:149)). `/integrations` is a bypass. |
| `POST/PUT/DELETE /mcp-connections` + `/commit` | [McpConnectionsController.cs:56](../../src/R2WAI.Api/Controllers/McpConnectionsController.cs:56) | A user can register an external MCP server and commit its tools for agents to call. |
| `POST/PUT/DELETE /approvals/policies`, `/toggle` | [ApprovalsController.cs:120](../../src/R2WAI.Api/Controllers/ApprovalsController.cs:120) | A user can create an active policy with `ApproverRoles="User"` and then approve other people's requests. This is privilege escalation of the approval control itself. |
| `PUT/DELETE /assistants/prompt-templates/{type}` | [AssistantsController.cs:143](../../src/R2WAI.Api/Controllers/AssistantsController.cs:143) | Changes the **tenant-wide** default system prompt for every agent of that type (prompt-injection vector). |
| `POST/PUT/DELETE /assistants`, `/publish` | [AssistantsController.cs:81](../../src/R2WAI.Api/Controllers/AssistantsController.cs:81), [:170](../../src/R2WAI.Api/Controllers/AssistantsController.cs:170) | Allowing "makers" to build agents can be a valid product choice (Copilot Studio does this), but then the UI should show it, and publish should be gated. |
| `POST/PUT/DELETE /chatbots`, `/knowledgebases`, `/applications` (+ `/discover`), `/business-capabilities`, `/navigation`, `/testcases` | [ChatbotsController.cs:50](../../src/R2WAI.Api/Controllers/ChatbotsController.cs:50), [KnowledgeBasesController.cs:14](../../src/R2WAI.Api/Controllers/KnowledgeBasesController.cs:14), [ApplicationsController.cs:15](../../src/R2WAI.Api/Controllers/ApplicationsController.cs:15), [NavigationController.cs:14](../../src/R2WAI.Api/Controllers/NavigationController.cs:14) | Publishing surfaces, knowledge and connected systems are all writable by any user. |

The client also has **no route-level role guard**: [router.tsx](../../src/R2WAI.Client/src/app/router.tsx) wraps everything in `RequireAuth` only, so a `User` who opens `/users`, `/models` or `/security` gets a page full of 403 error states instead of a clean "not allowed" screen.

**Fix (M):** first decide the role model (who may build, who may publish, who may govern). Then:
- add `[Authorize(Roles = "Admin,SystemAdmin")]` (or named policies such as `CanBuildAgents`, `CanPublish`, `CanGovern`) to every write action listed above;
- make `/integrations` writes require the same role as `/capabilities`;
- add a `RequireRole` route wrapper in the client that reuses `roleNav` `visibleTo`;
- extend `RoleMatrixSecurityTests` to cover every write route for the `User` account (today it covers 8 routes).

### 1.2 P0 — Egress guard blocks on-prem integration and is still bypassable

[EgressGuard.cs:30-73](../../src/R2WAI.Infrastructure/Security/EgressGuard.cs:30) is applied to integrations, MCP and tool dispatch (9 call sites).

- **Product blocker:** it always refuses 10/8, 172.16/12, 192.168/16, link-local and `.internal/.local/.corp` hosts, with **no allow-list** (its own comment, lines 22-26, notes this is still open). An on-prem customer's ERP at `10.20.1.5` or `erp.corp` cannot be connected at all.
- **Bypasses:** it checks only the host *string*. A public name that resolves to a private IP (DNS rebinding, `*.nip.io`) passes. HTTP redirects are followed with no re-check (no `AllowAutoRedirect`/`ConnectCallback` handling anywhere in the repo). CGNAT `100.64/10`, `0.0.0.0/8` and IPv4-mapped IPv6 are not covered.

**Fix (M):** replace the string check with a `SocketsHttpHandler.ConnectCallback` that resolves DNS and validates the **actual connected IP**, disable automatic redirects (or re-validate each hop), and add `Egress:AllowedNetworks` / `Egress:AllowedHosts` config (CIDR list, per tenant optionally) so on-prem admins can explicitly permit internal systems. Default-deny stays; explicit allow becomes possible.

### 1.3 P1 — Webhook trigger weaknesses

In [WorkflowsController.cs:327-414](../../src/R2WAI.Api/Controllers/WorkflowsController.cs:327):
- **Cross-tenant trigger (legacy path):** when no `WebhookEndpoint` row matches, it falls back to one **global** `Webhooks:Secret` shared by every tenant and looks up `Workflow.Trigger == slug` across all tenants (line 379). Anyone holding the shared secret can start another tenant's workflow by guessing its trigger text.
- **HMAC is computed over a re-serialized body** (`JsonSerializer.Serialize(payload)`, line 394), not the raw bytes the sender signed. Real senders' signatures (GitHub/Stripe style) will not match because of whitespace or key order, so signed mode is unusable in practice.
- **Secrets stored in plaintext** (`WebhookEndpoint.Secret`), unlike MCP credentials which are encrypted.
- **Editing wipes the secret:** `WebhookEndpoint.Update` sets `Secret = secret` unconditionally ([WebhookEndpoint.cs:35-41](../../src/R2WAI.Domain/Entities/WebhookEndpoint.cs:35)), so a `PUT` without a secret silently disables the webhook (it then returns 503).

**Fix (S-M):** remove the global-secret fallback, read the raw body (`EnableBuffering`) for HMAC, encrypt secrets with the existing `IEncryptionService`, keep the existing secret when the update omits it, and require a secret at creation.

### 1.4 P1 — Rate limiting is effectively off

[RateLimitingMiddleware.cs:16](../../src/R2WAI.Api/Middleware/RateLimitingMiddleware.cs:16) applies one global limit of **10,000 requests/minute per client** and its Redis counter is get-then-set (not atomic). Anonymous LLM endpoints (`/chatbots/{id}/chat`, `/chat/stream`, `/webhook`, `/messages/attachment`), `forgot-password` and `request-access` have no tighter limit. One script can run up the GPU/LLM bill or flood email.

**Fix (S):** use ASP.NET Core's built-in `AddRateLimiter` with named policies (for example: anonymous chat 20/min per IP+chatbot, auth 5/min per IP, default 300/min per user) and Redis `INCR` + `EXPIRE` for the distributed counter.

### 1.5 P1 — Anonymous upload trusts the declared content type

[ChatbotsController.cs:604-638](../../src/R2WAI.Api/Controllers/ChatbotsController.cs:604) checks size and the **client-declared** `ContentType`; the origin check relies on a header any non-browser client can set. There is no magic-byte sniffing and no per-chatbot storage quota. **Fix (S):** sniff file signatures, enforce a per-chatbot/day quota, and serve uploads with `Content-Disposition: attachment`.

### 1.6 P1 — Remaining cross-tenant and logic paths

All 72 `IgnoreQueryFilters()` uses were scanned. Most are ID-scoped background or anonymous lookups by design. Exceptions:
- Legacy webhook lookup by `Trigger` across tenants (§1.3).
- `VerifyApproverAuthorization` takes **whichever** active approval policy it finds first (`FirstOrDefaultAsync`, [ApprovalService.cs:294-296](../../src/R2WAI.Infrastructure/Services/ApprovalService.cs:294)), not the policy that created the request. With more than one active policy, who may approve is nondeterministic. **Fix (S):** store the `ApprovalPolicyId` on the request and check against that policy.

### 1.7 Security items verified as done (no action)

Separation of duties on approvals ([ApprovalService.cs:165](../../src/R2WAI.Infrastructure/Services/ApprovalService.cs:165)); fail-closed tenant query filter; integration credentials redacted in API responses (`IntegrationCredentialCodec.Redact`); MCP credentials encrypted; security headers (CSP, HSTS, X-Frame-Options, nosniff) in `SecurityHeadersMiddleware`; production start-up blocks on missing or placeholder JWT/encryption/DB secrets ([Program.cs:537-601](../../src/R2WAI.Api/Program.cs:537)); dev-only seeding endpoint double-gated; globally unique user e-mail index.

---

## 2. Features that look complete but do nothing (P0 for a pilot)

| Feature | What the user sees | What actually happens | Evidence |
|---|---|---|---|
| Automation triggers "Schedule", "Application Submitted/Updated", "Payment Received", "Form Submitted" | Selectable in the wizard and offered by "Draft with AI" | **Never fire.** No scheduler or event source exists. The only hosted services are escalation, delay-resume, data-retention and the job queue. Only manual run, webhook and the AI `start_workflow` tool start a workflow. | [WorkflowsController.cs:69](../../src/R2WAI.Api/Controllers/WorkflowsController.cs:69), [AutomationDetailPage.tsx:34](../../src/R2WAI.Client/src/features/automations/pages/AutomationDetailPage.tsx:34), [Program.cs:105-107](../../src/R2WAI.Api/Program.cs:105) |
| "Action" step (Verify Documents, Assign Officer, Update Application Status, Create Task) | Step runs and shows success | **Writes one log line** and nothing else | [ActionNodeProvider.cs:11](../../src/R2WAI.Api/Workflows/NodeProviders/ActionNodeProvider.cs:11) |
| Content moderation | API to save rules | Rules saved to tenant JSON; **read by nothing**; no UI either | [AdminController.cs:467-540](../../src/R2WAI.Api/Controllers/AdminController.cs:467) |
| Teams / Slack / WhatsApp / SMS channels | Channel enum, WhatsApp setup wizard | **No adapter** for any of them; the WhatsApp page itself warns "not deployed" | [ChatbotChannelType.cs](../../src/R2WAI.Domain/Enums/ChatbotChannelType.cs), [WhatsAppSetupPage.tsx:221](../../src/R2WAI.Client/src/features/deploy/pages/WhatsAppSetupPage.tsx:221) |
| Navigation management API | Full CRUD + reorder | Client navigation is hard-coded in `roleNav.ts`; **no caller** | [NavigationController.cs](../../src/R2WAI.Api/Controllers/NavigationController.cs) |
| Access requests | Anonymous `POST /auth/request-access` + admin approve/reject | **No UI on either side** (login page has no link, admin has no queue). It is pure anonymous attack surface. | [AuthController.cs:549](../../src/R2WAI.Api/Controllers/AuthController.cs:549) |

**Fix:** for each row, either implement it or remove it from the UI and API. Recommended:
- **Schedule trigger:** implement (Elsa.Scheduling is already referenced) with a lease-safe cron sweeper.
- **Event triggers:** replace the government-specific labels with real ones: "Record created in connected system" (polling or webhook), "Form submitted" (widget form), "Message received".
- **Action step:** remove it from the AI draft vocabulary and map those intents to real steps (API Call against a connected system, Approval, Email).
- **Content moderation, Navigation API, access requests:** delete them unless a UI and enforcement are built.
- **Messaging channels:** hide them until at least Teams and WhatsApp Cloud adapters ship.

---

## 3. CRUD and lifecycle completeness

The cross-reference found **0 broken client calls** but **~60 backend operations with no UI**. The ones that matter for daily use:

| Area | Missing in the UI | Backend already supports |
|---|---|---|
| Automations | **Unpublish, archive, restore, versions, new version, bulk delete**. Once published, an automation cannot be taken offline from the UI. | `workflows/{id}/unpublish`, `/archive`, `/restore`, `/new-version`, `/versions`, `/bulk-delete` |
| Knowledge | **Test search** ("ask this knowledge base"), versions and rollback, bulk delete, source listing | `knowledgebases/{id}/search`, `/versions`, `/rollback`, `/bulk-delete` |
| Agents | Prompt-template management (tenant defaults), prompt history, archive | `assistants/prompt-templates`, `/prompt-history`, `/archive` |
| Approvals | **Approval policy management** (who approves, escalation, SLA) | `approvals/policies` CRUD + toggle |
| Webhooks | **Edit** (only create/toggle/delete exist in `DeveloperTab.tsx`) | `PUT admin/webhooks/{id}` |
| Documents | Summarize, extract, ask, reprocess, download | `documents/{id}/summarize`, `/extract`, `/ask`, `/process`, `/download` |
| Admin | Bulk delete (users, models), model API-key rotate/remove, analytics, data-retention run | corresponding `admin/*` routes |
| Users | Invite flow; set roles during creation (today a two-step create, then assign) | `admin/users/invite` |

**Fix:** prioritise automation lifecycle, KB test-search, approval policies and webhook edit (all S-M, backend done). Delete the backend routes that will not get a UI (§7).

---

## 4. UX quality: forms, validation, feedback, notifications

### 4.1 Validation

- **Server:** FluentValidation covers **83 of 103** commands. The 20 without a validator are mostly ID-only delete/toggle commands, which is acceptable. However, **11 controllers bypass MediatR** and therefore bypass the validation and authorization pipeline entirely: `ApiKeys`, `Approvals`, `McpConnections`, `Notifications`, `Runs`, `Webhooks` (0 MediatR calls), and the fat `Auth` (796 lines), `Chatbots` (769), `Admin` (754), `Operations` (586) and `Workflows` (499), which mix direct `DbContext` access with commands. **Fix:** move write paths in these controllers into commands with validators.
- **Client:** `zod` and `react-hook-form` are dependencies, but only ~6 of 28 create/edit dialogs use them. The rest hand-roll `useState` checks of uneven depth: some validate e-mail, URL or number ranges (`CreateEditUserDialog`, `CreateEditMcpConnectionDialog`, `CreateEditModelDialog`), others only check "not empty". Server 422 field errors are surfaced as one toast line (`apiErrorMessage`), not mapped onto the fields.

### 4.2 Ease of use (concrete examples)

- **Webhook dialog** ([CreateEditWebhookDialog.tsx:40-47](../../src/R2WAI.Client/src/features/admin/dialogs/CreateEditWebhookDialog.tsx:40)): "Trigger type" is free text and "Workflow ID" asks the admin to paste a raw GUID instead of choosing an automation from a list. "Secret" is optional, yet a webhook without a secret always returns 503.
- **User creation** requires a free-text "External ID" and cannot set roles or send an invite in the same step.
- **Pages without an error state** when their queries fail: `UsersPage`, `ApplicationsPage`, `AutomationsListPage`, `ChatbotsPage`, `HomePage`, `ToolsListPage`. Pages without an empty state: `ChatbotsPage`, `RunsPage`, `SecurityPolicyCenterPage`, `SettingsPage`, `WhatsAppSetupPage`. Create dialogs that load lookups with no loading or error handling: `CreateAssistantDialog`, `CreateAutomationDialog`, `CreateChatbotDialog`, `CreateEditTestCaseDialog`.
- **28 `set-state-in-effect` lint warnings**, the pattern that causes flicker and double renders in dialogs that reset their form on open.
- **English-only UI:** there is no i18n library. For the government/India segment the product targets (Aadhaar/PAN PII rules are built in), Hindi and regional-language UI and agent responses are an expected requirement.

### 4.3 Notifications

Only four events create a user notification: document processed, approval requested, approval decided, and one AI workflow plugin call (`grep SendAsync`). **Not notified:** automation run failed or completed, KB indexing failed, integration or MCP health failure, model provider down, usage cap reached, user invited or role changed, API key expiring.

`NotificationService.SendToTenantAsync`, `BroadcastAsync` and `SendToGroupAsync` ([NotificationService.cs:79-112](../../src/R2WAI.Infrastructure/SignalR/NotificationService.cs:79)) have **no callers** and push a *different* payload shape on the same `ReceiveNotification` event than `SendAsync` does, so they would be misread by the client if anyone started using them.

**Fix (M):** add a small event-to-notification map (run failed → owner; KB index failed → owner; connector test failing → admins; quota 80%/100% → admins), add per-user notification preferences, and delete or align the three dead broadcast methods.

---

## 5. Deployment and operations (P0 / P1)

| Issue | Evidence | Fix |
|---|---|---|
| **Postgres published on the host in the production compose** | [docker-compose.production.yml:122-123](../../docker/docker-compose.production.yml:122) `5432:5432` | Remove `ports`; DB reachable only on the internal network |
| API port published directly (bypasses nginx) | [docker-compose.production.yml:26-27](../../docker/docker-compose.production.yml:26) `5000:8080` | Expose only the gateway |
| Redis without password (compose and K8s) | [docker-compose.production.yml:96](../../docker/docker-compose.production.yml:96), [k8s/redis.yaml:40](../../k8s/redis.yaml:40) | `--requirepass` from a secret; TLS if cross-host |
| Default compose runs **Production** with **demo seeding** (public passwords `admin@r2wai.io` …) | [docker-compose.yml:26,31](../../docker/docker-compose.yml:26) | Seeding in Production should **throw**, not warn ([DatabaseInitializer.cs:94-96](../../src/R2WAI.Infrastructure/Persistence/DatabaseInitializer.cs:94)); dev compose should use `Development` |
| No TLS at the gateway (nginx `listen 8080` plain HTTP) | [docker/nginx/studio.conf:16](../../docker/nginx/studio.conf:16) | TLS termination config + HTTP→HTTPS redirect for on-prem installs |
| **High-severity vulnerable packages in production projects**: `Microsoft.OpenApi.Readers 1.6.22` (it parses *untrusted* OpenAPI specs during import), `System.Security.Cryptography.Pkcs 10.0.7` | `dotnet build` NU1903 in R2WAI.Api / R2WAI.Infrastructure | Upgrade both. The 2026-10-01 readiness report's "no vulnerable production packages" is now stale. |
| CI vulnerability gate fails only on "Critical" | [ci.yml:166](../../.github/workflows/ci.yml:166) | Fail on High too |
| CI lint step exits 1: oxlint flags Playwright's fixture `use()` as a React hook | [e2e/fixtures/cleanup.ts:32](../../src/R2WAI.Client/e2e/fixtures/cleanup.ts:32), [.oxlintrc.json](../../src/R2WAI.Client/.oxlintrc.json) | Add an override or `ignorePatterns` for `e2e/**` |
| `:latest` image tags; hosted sweepers not lease-safe above 1 replica | [k8s/api-deployment.yaml:47](../../k8s/api-deployment.yaml:47) | Pin digests; add leases to the remaining sweepers (delay-resume already has one) |
| Backup/restore scripts never exercised (from the 2026-10-01 report, still true) | `docker/backup.sh`, `restore.sh` | Run a restore drill; record RPO/RTO |

---

## 6. Copilot Studio comparison (feature by feature)

Legend: ✅ comparable · 🟡 partial · ❌ missing. The R2WAI column reflects what was verified in code today.

| Capability | Copilot Studio (2026) | R2WAI today | Gap / action |
|---|---|---|---|
| Deployment | Cloud only | ✅ Docker/K8s on-prem, air-gap capable, local models (Ollama) | **Differentiator.** Must fix §1.2 or it cannot reach internal systems. |
| Model choice | Microsoft-managed, a few model families | ✅ OpenAI / Ollama / Zai per agent via ModelGateway | Add fallback/routing between models |
| Agent instructions, starters, tone | ✅ | ✅ Studio tabs (Overview, Knowledge, Tools, Channels, Analytics, Delegates) | — |
| Test pane while building | ✅ | ✅ Docked live preview | — |
| Versions / rollback for agents | ✅ (solutions) | ✅ Backend + Studio | — |
| **Topics / dialog designer** (trigger phrases, nodes, variables, conditions, cards) | ✅ Core feature | ❌ None (no Topic concept) | Add a "Topics" canvas reusing the `@xyflow` automation builder, or position as generative-only |
| Generative orchestration (AI picks tools/knowledge) | ✅ Default | 🟡 SK auto function calling over the agent's enabled tools | Add tool/knowledge descriptions as routing hints; show the chosen plan in the test pane |
| **Multi-agent** | ✅ Child and connected agents | 🟡 `delegate_to_agent` with a depth cap (new in `22678c28d`) | Add parallel fan-out + aggregation (supervisor) and show the delegation trace in Runs |
| **Autonomous / event-triggered agents** | ✅ Event triggers | ❌ Agents only answer chat; automation triggers mostly don't fire (§2) | Implement schedule + connector-event triggers that can start an agent run |
| Agent flows / automations | ✅ Power Automate, hundreds of actions | 🟡 Canvas with 9 step types (Approval, AI Generate, Email, API Call, Condition, Delay, Transform, Log, no-op Action) | Add: call agent, call integration/MCP tool, loop/for-each, parallel, set variable, human task/form |
| Knowledge sources | ✅ SharePoint, OneDrive, Dataverse, websites (crawled), files, connectors | 🟡 Files, single-fetch URL, text; pgvector with tenant column | Scheduled re-crawl, site crawl, DB/file-share connectors, **KB test-search UI**, citations in UI (widget already has them) |
| Connectors | ✅ 1,400+ governed connectors, MCP, REST, custom | 🟡 9-entry catalogue, OpenAPI import, HTTP/SSE MCP, direct REST | Grow the catalogue with on-prem-relevant templates (SAP, Oracle EBS, ServiceNow, Jira, SQL Server/Postgres, LDAP, SMTP, SFTP); add an internal-network allow-list (§1.2) |
| Tool auth schemes | ✅ OAuth, API key, Entra | 🟡 None / Bearer / Basic / API-key header, credentials encrypted at rest; OAuth2/Entra treated as a pasted static token ([DynamicToolExecutor.cs:108-188](../../src/R2WAI.Infrastructure/AI/DynamicTools/DynamicToolExecutor.cs:108)) | Add OAuth2 client-credentials with token refresh, and an external secret-vault reference |
| Human-in-the-loop approvals | ✅ | ✅ Approvals with separation of duties, escalation, park/resume for deferred tool calls | Add policy-management UI (§3) and payload preview on the approval card |
| **Human hand-off to a live agent** | ✅ Omnichannel, ServiceNow, Salesforce, Genesys | ❌ None | Add a "transfer to human" tool + inbox queue in Approvals/Inbox, or an outbound hand-off webhook |
| **Channels** | ✅ Teams, M365 Copilot, web, SharePoint, WhatsApp, Facebook, Slack, telephony | 🟡 Website widget, generic HTTP webhook, REST API | Teams and WhatsApp Cloud adapters first (Bot Framework / Meta Graph), then Slack |
| **End-user authentication** in published agents | ✅ Entra / OAuth sign-in | ❌ Widget is anonymous only | Signed widget token (JWT from host site) so agents can act on behalf of the end user |
| Voice | ✅ Telephony / IVR | 🟡 Browser speech-to-text in the widget, no TTS | TTS output; SIP/telephony later |
| Analytics | ✅ Sessions, resolution, escalation, CSAT, topic themes | 🟡 Per-chatbot usage, thumbs feedback, Monitor KPIs, token counts | Add resolution/escalation/CSAT funnels and unanswered-question clustering |
| Evaluation / test sets | ✅ Agent evaluation | 🟡 Test cases + runs in Monitor | Add LLM-judged scoring and regression gating before publish |
| ALM | ✅ Solutions, environments, pipelines | ❌ No export/import, no dev→test→prod promotion | Agent/automation export-import as JSON packages; environment promotion |
| Governance | ✅ DLP, Purview, environments, RBAC | 🟡 6 policy types enforced (AiUsage, ToolExecution, DataRetention, PII, Approval, Knowledge), audit log, API-key scopes, MFA, Entra SSO login | Fix RBAC (§1.1); implement or delete content moderation (§2) |
| Multilingual | ✅ | ❌ English-only UI | i18n framework + Hindi first |

**Bottom line:** R2WAI is comparable in concept coverage and ahead on sovereignty. To credibly claim "Copilot Studio for on-prem", the minimum set is: §1 security fixes, §2 honesty fixes, an internal-network allow-list, Teams + WhatsApp adapters, schedule/event triggers, human hand-off, KB test-search, and ALM export/import.

---

## 7. Duplicate and dead code

### 7.1 Duplicate concepts (confusing UX + security drift)

Five overlapping concepts for "something an agent can call":

| UI area | API | Entity |
|---|---|---|
| Tools & APIs (`/tools`) | `/capabilities` (create = SystemAdmin) | `ToolDefinition` |
| Integrations (`/integrations`) | `/integrations` (create = any user) | **same** `ToolDefinition` |
| Connections (`/connections`) | `/applications` + `/apis` | `ConnectedApplication` + `ApplicationApi` |
| MCP Servers (`/mcp-connections`) | `/mcp-connections` | `McpServerConnection` → commits `ToolDefinition` |
| Capabilities tab | `/business-capabilities` | `BusinessCapability` |

Two controllers writing one table with different rules is exactly how §1.1's bypass appeared. Copilot Studio presents a single **Tools** surface (connector, REST, MCP, flow, prompt). **Fix (L):** one "Tools" page with a source picker (Connected system · REST/OpenAPI · MCP · Automation), one controller per entity, one authorization policy.

Other duplication:
- **Two approval-policy systems:** the `ApprovalPolicy` entity (workflow approvals) and the `GlobalPolicy` "Approval" type (AI tool approvals).
- **Four chat surfaces:** Playground, Studio test pane, CopilotPanel, conversations.
- **Two workflow runtimes in play:** Elsa 3.7 (still referenced by `R2WAI.Api.csproj`) with an earlier decision recorded to exit Elsa, plus SK and Microsoft Agent Framework for agents. Decide and record in an ADR.

### 7.2 Dead or unreachable code (remove or wire)

| Item | Evidence |
|---|---|
| Navigation feature (controller, 4 commands, `NavigationDefinition` entity + table) | No client caller; nav is static |
| Content-moderation endpoints + settings record | Read by nothing (§2) |
| `NotificationService.SendToTenantAsync`, `BroadcastAsync`, `SendToGroupAsync` | No callers; wrong payload shape |
| Access-request endpoints (`request-access`, admin approve/reject) | No UI either side |
| Departments API | UI removed 2026-10-09; kept only for FKs. Plan a migration to drop it. |
| `chat/suggested-actions`, `operations/health`, `operations/health/detailed`, `operations/workflow-instances`, `published-assistants/*`, `admin/analytics` | No client caller (confirm external consumers before deleting) |
| `components/LegacyNotice.tsx` | Imported only by its own test |
| 2 `CS9113` unused primary-constructor parameters, 11 `CS0618` obsolete-API uses | `dotnet build` warnings |

Client dead-file scan otherwise came back clean (the 2026-10-12 cleanup held).

---

## 8. What was verified, and what was not

**Run in this session (2026-10-15):**
- `dotnet build R2WAI.slnx -c Release`: 0 errors, 360 warnings (unique: 132 IDE0290, 66 IDE0161, 60 CS8618, 11 CS0618, plus the NU1903/NU1902 package advisories listed in §5).
- Unit tests: Domain **205/205**, Application **162/162**, Infrastructure **405/406**. The single failure (`BuiltInToolGovernanceTests.The_default_table_lists_only_functions_that_actually_exist`) comes from **another session's uncommitted User Memory work** that appeared in the working tree during this audit, not from committed code.
- Client: `tsc -b` clean; vitest **126/126**; oxlint 0 real errors, 1 false-positive error (§5), ~35 warnings.
- Static scans: endpoint inventory (306), client→API cross-reference (180 calls, 0 broken), validator coverage, `IgnoreQueryFilters` scan, page state scan, dead-file scan.

**Not run (Docker Desktop was down):** `R2WAI.Api.Tests` (Testcontainers), Playwright e2e, live UI walkthrough, live chat with a model. Findings in §1-§2 are code-verified; exploitability against a running stack (for example a `User` token calling `POST /approvals/policies`) should be confirmed with a live request as part of the fix.

---

## 9. Recommended fix order

| # | Work | Size | Unblocks |
|---|---|---|---|
| 1 | API RBAC hardening (§1.1) + role-matrix tests for every write route + client route guard | M | Security review |
| 2 | Egress: resolve-and-check IP, no blind redirects, `Egress:AllowedNetworks` (§1.2) | M | On-prem integration (the core pitch) |
| 3 | Deployment defaults: close DB/API ports, Redis auth, fail on demo seed in Production, TLS sample, upgrade 2 vulnerable packages, CI gate on High, oxlint e2e override (§5) | S | Any customer install |
| 4 | Feature honesty: real Schedule trigger, remove or replace fake triggers and the no-op Action step, hide unbuilt channels, delete content moderation (§2) | M | Pilot trust |
| 5 | Webhook fixes: raw-body HMAC, encrypted secret, no global fallback, keep secret on edit, edit UI with an automation picker (§1.3, §4.2) | S | Integrations |
| 6 | Lifecycle UI: automation unpublish/archive/versions, KB test-search, approval-policy management (§3) | M | Daily operations |
| 7 | Notifications event map + preferences (§4.3) | M | Operations |
| 8 | Dead-code removal (§7.2) and the Tools consolidation design (§7.1) | S + L | Maintainability |
| 9 | Copilot Studio parity: Teams + WhatsApp adapters, human hand-off, end-user auth in widget, ALM export/import, i18n (§6) | L each | Market parity |
