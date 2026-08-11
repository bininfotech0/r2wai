# R2WAI Enterprise Audit Report — Pre-Release Hardening Pass

**Date:** 2026-07-14
**Scope:** Targeted hardening pass, not a full re-audit. Verified every finding from the prior audit (commit `db2652f84`, 2026-06-27) against current code, fixed the real issues found (including one new critical bug uncovered only through live testing), and live-verified the fixes against a running docker-compose stack with a real Postgres database and a real local Ollama model.
**Method:** Static code review + live execution (docker-compose stack, real Postgres, real Ollama `qwen2.5-coder:7b`, real browser via Playwright). Not a re-run of the full 20-section literal audit prompt — see "Out of scope" below for why, and what that means for how to read this report.

---

## Addendum (2026-08-10) — Strategic re-direction

This report is a **historical snapshot** of the hardening pass and remains accurate for the codebase at commit time. Since then, the product strategy has pivoted to an **Application-centric Government AI Platform** (see [ARCHITECTURE.md](ARCHITECTURE.md) and [ROADMAP.md](ROADMAP.md)). How that changes the reading of this report:

- **The "missing features" list is now partially obsolete by design.** Studios and concepts flagged here as standalone features (chatbot, model, integration, navigation, tool, media) are being **removed or merged** — `Chatbot` merges into `Assistant → Channels`, standalone studios fold into Application areas, media/creative features are removed from the government core. These are no longer net-new features to build; they are refactors to remove.
- **New P0 priorities supersede part of this report's roadmap.** The pivot's P0 list — `Application`/`Department` entities + migrations, real frontend↔backend integration, complete KB indexing (the text/URL source gap identified in section 5 remains open), workflow step chaining, real approval UI, tenant isolation verification, RBAC hardening + **ABAC**, and a **Tool/API Gateway** — replaces the audit's "missing enterprise features" roadmap as the ordering principle.
- **The Tool Framework becomes the Tool/API Gateway.** Section 4's note that tool-framework auth is admin-gated and internal is being replaced by explicit per-tool enforcement (role, permission, risk level, confirmation, approval, audit) so the LLM never calls government APIs directly.
- **The "suitable for a single-tenant internal pilot" verdict still holds** for the audited codebase, but the target is now a department-scoped government pilot under the pivoted plan. The open items this report flags — concurrency tokens, RAG indexing gap, audit export — are all carried into the pivot's Phase 0/6 work.

The rest of this report is preserved unmodified as the record of the 2026-07-14 pass.

---

## 1. Where this leaves the product

The prior audit (`db2652f84`) gave R2WAI a 46/100 readiness score and a hard **NO-GO**, citing 10 critical (P0) bugs. Four commits landed between that audit and this pass. Verifying against current code: **8 of 10 P0 bugs were already fixed**, along with most of the high-priority (P1) security findings (IDOR, timing attacks, MFA bypass, CORS, rate-limit races, SignalR auth, tenant header spoofing, hardcoded seed password). That earlier NO-GO verdict is now **stale** — it describes a version of the codebase that no longer exists.

This pass fixed the remaining real issues (2 open P0-equivalent security bugs, four **new bugs this pass discovered via live testing that no prior audit had found** — including a critical break in the core RAG ingestion path and a systemic bug that was silently failing ~85% of the API integration test suite — plus several P1 reliability/UX bugs) and verified all of it against a real running stack. It explicitly did **not** attempt to rebuild every missing enterprise feature (SSO providers, billing, compliance evidence, 12+ unbuilt integrations) — those are net-new projects, not bugs, and are listed as a roadmap at the end instead of being attempted here.

**Current assessment: conditionally viable for a single-tenant internal pilot**, and — unlike at the start of this pass — **the test suite can now actually be trusted as a CI signal** (407/407 passing across the whole solution, up from 233 failing at the start of this pass). Not yet ready for regulated/multi-tenant-at-scale production — see "Remaining gaps" below.

---

## 2. Already fixed since the prior audit (verified, no action needed this pass)

Permission enum overflow, broken flags parsing, `${VAR}` config templates, fire-and-forget approval tasks, approval-authorization bypass, path traversal in chat upload, missing file-size limit, no brute-force protection, IDOR on documents/workflow steps, timing attacks on token comparison, MFA exception-bypass, most SSRF vectors, CORS localhost fallback in production, rate-limit race condition, SignalR hub authorization, tenant-header spoofing, anonymous Prometheus endpoint, hardcoded seed password, domain-event dispatch-then-clear ordering, chatbot webhook key persistence, chart NaN/empty-data crashes, in-memory cache eviction leaks, and theme persistence (the last one was fixed but not documented — see finding #10 below for a related bug this pass found in the *same* code).

## 3. Fixed in this pass

### Security
1. **JWT parsed without signature validation in Blazor Server** — `src/R2WAI.Web/Authentication/JwtAuthenticationStateProvider.cs`. The Blazor Server host trusted client-controlled token storage without verifying the signature (R2WAI.Web has no JWT signing key by design — only the API does). Fixed by corroborating the token against the API's `GET /api/v1/auth/me` once per circuit, with the result cached for that token so it's not a round-trip on every render. A forged/tampered token is now rejected and clears the session. **Live-verified**: a bit-flipped valid token now returns 401 from `/auth/me` and the app treats it as logged out.
2. **Stored XSS in chatbot embed script** — `src/R2WAI.Web/Components/Dialogs/ChatbotEmbedDialog.razor`. The tenant-editable chatbot title was concatenated raw into both an HTML attribute and a JS string literal inside the generated `<script>` embed snippet every tenant copies onto their own website. Fixed with `WebUtility.HtmlEncode`. **Live-verified** via Playwright: a title of `Test"><script>alert(1)</script>` now renders in the generated snippet as literal escaped text (`&quot;&gt;&lt;script&gt;...`), not a live `<script>` tag.
3. **SSRF loopback allowlist in `AdminController.IsAllowedEndpoint`** — reviewed, not changed. It intentionally allows `localhost`/`host.docker.internal` because the product's default AI provider is self-hosted Ollama on the same host; blocking loopback would break that. The endpoint is `[Authorize(Roles=Admin)]`-gated, so this is an accepted admin-trusts-admin risk, not anonymous SSRF. Documented here rather than silently "fixed" in a way that would break local-model support.

### Reliability
4. **In-memory pagination** — `ApprovalsController.GetPendingByRole` loaded every pending approval for a role into memory before paging. Now uses the existing `GetPendingPagedAsync(role: ...)` overload, which pages in the database query.
5. **`RedisCacheService` never disposed its `ConnectionMultiplexer`** — now implements `IDisposable`.
6. **`DatabaseInitializer` fell back to `EnsureCreatedAsync()`** (bypasses EF migrations, permanently diverging the DB from the normal migration path) on the first connection hiccup. Replaced with a bounded retry of the real connection. **Live-verified the fix twice over**: the first retry budget (10×3s) was caught being too short during actual docker-compose testing — Postgres was marked "healthy" by Compose but still refused connections for ~27s past that point on a cold start. Widened to 20×5s and confirmed a clean init afterward. This is a case where live testing caught my own fix being under-provisioned before it shipped.
7. **`SemanticKernelService` cloned the AI kernel on every request**, including every anonymous chatbot message. The clone was only ever needed for the tool-enabled path (which attaches request-scoped plugins); the no-tools path now returns the shared base kernel directly instead of cloning it for no reason.

### AI layer
8. **No retry/resilience on LLM API calls** — Polly-style resilience only covered the generic tool-calling HTTP client, not the actual chat/embedding calls to OpenAI/Ollama/Z.ai. Added `ClientRetryPolicy` to all three provider client configurations.
9. **No input token limit** — prompt/context/chat-history were concatenated unbounded before every LLM call (a cost and OOM risk with no protection at all). Added a coarse (~4 chars/token) truncation guard applied to RAG context, chat history, and all the direct-text AI operations (summarize/extract/compare/answer-question).

### UI
10. **Dark mode was a server-wide singleton, not per-user** — `ThemeService` was registered `AddSingleton`, so one user's dark-mode toggle flipped the theme for every other concurrently connected user on the server. This was found by reading the DI registration while investigating the reported "theme not persisted" bug (persistence itself was already correctly implemented via localStorage in `MainLayout.razor` — the real, more severe bug was the cross-user state leak). Fixed by changing to `AddScoped`. **Live-verified** with two separate browser sessions: toggling dark mode in session A left a fresh session B in light mode, and refreshing session A correctly kept it dark.
11. **Edit-user dialog loaded the entire user list client-side to find one user by ID.** Added `GET /api/v1/admin/users/{id}` (tenant-scoped, following the existing `GetUsersQuery` pattern) and pointed the dialog at it. **Live-verified**: dialog opens and populates correctly.
12. **Markdown live-preview used a hand-rolled regex chain** that broke on nested formatting and didn't support links or tables. Replaced with the same Markdig pipeline + sanitization already used by `MarkdownRenderer.razor`, so live preview now matches actual rendering elsewhere in the app.

### Critical bugs found only through live testing (not in any prior audit)
13. **Adding any source (text/URL) to a knowledge base always failed with a 500** — `AddSourceCommandHandler` relied solely on EF Core's collection-navigation fixup (`kb.AddSource(source)`) to track the new `KnowledgeBaseSource` entity, without ever explicitly adding it to the DbSet. This produced a `DbUpdateConcurrencyException` ("expected to affect 1 row, actually affected 0") on **every single call, 100% reproducible**, despite no concurrency tokens being configured anywhere in the system. This is a core RAG-pipeline entry point — every path for growing a knowledge base with new content beyond the initial file upload was completely broken. Root-caused by bisecting (ruled out DB triggers, ruled out `EnableRetryOnFailure`, confirmed structurally-identical patterns elsewhere in the codebase — e.g. `ChatService.SendMessageAsync` adding a child `Message` to a `Conversation` — work fine) down to the missing explicit track call. Fixed by using the already-injected-but-previously-unused `IRepository<KnowledgeBaseSource>.AddAsync(source)` alongside the domain method. **Live-verified**: reproduced the failure on a fresh KB, applied the fix, rebuilt, and confirmed `201 Created` with the row actually persisted in Postgres.
14. **`RedisCacheService`'s constructor could take down the entire API on any request** — `ConnectionMultiplexer.Connect(connectionString)` was called with default options, which throw synchronously if Redis is unreachable at connect time. Since `ICacheService` is a singleton and `RateLimitingMiddleware` (which resolves it on literally every request, before authentication even runs) has no try/catch around resolving it — only around using it — a single failed Redis connection attempt turned into every request 500ing, not the graceful in-memory-rate-limit fallback the middleware's own code clearly intended (and already correctly implements one layer down). Root-caused this exact failure mode live: the API test suite's `WebApplicationFactory` doesn't run a Redis container, and `appsettings.json` hardcodes a default `localhost:6379` connection string with nothing to override it in the test environment, so **every one of the ~233 previously-failing `R2WAI.Api.Tests`** (see the correction in section 5) was failing for this one reason, not 233 different reasons. Fixed with the standard StackExchange.Redis remedy: `AbortOnConnectFail = false` via `ConfigurationOptions`, so a connect-time failure degrades gracefully into the existing per-call fallback instead of crashing the app. This is a real production resilience gap too, not just a test artifact — a brief Redis outage or restart would previously have taken the whole API down instead of degrading. **Live-verified**: full `R2WAI.Api.Tests` suite went from 233 failed / 41 passed to **274/274 passing** after this one fix, plus two follow-on test-assertion fixes for outcomes the old tests didn't account for (brute-force lockout's 429 during a theory's repeated login attempts; the webhook endpoint's correct fail-closed 503 when unconfigured). Combined with the other fixes in this report, the whole solution (API + Domain + Application + Infrastructure) is now 407/407 passing.

---

## 4. Explicitly deferred (with reasoning)

- **Concurrency tokens on all entities** — confirmed none exist anywhere. Adding `RowVersion` is a real, substantial, cross-cutting migration effort, not a drive-by fix.
- **`User.Status` / `Workflow.VersionStatus` as magic strings instead of enums** — mechanical but wide-blast-radius; better done deliberately.
- **Loading skeletons across all 43 Blazor pages** — only 1 page has them today; a UI pass unto itself.
- **CSRF/antiforgery dead code** — registered but unused; lower risk since the API is JWT-bearer (not cookie) based. Recommend removing the unused registration rather than wiring up CSRF that doesn't apply to this auth model.
- **The "missing features" roadmap from the prior audit** (SSO beyond Entra ID, billing/quotas, SOC2 evidence, mobile offline, visual workflow designer, Salesforce/SAP/ServiceNow/Jira/GitHub integrations, etc.) — net-new projects, not bugs. Not attempted here; still a valid roadmap.
- **Live testing of external integrations** (Teams/Slack/WhatsApp/SharePoint) — no credentials available in this environment; reviewed as code only, unchanged from prior audit's findings.

## 5. New gaps found this pass

- **`GET /api/v1/knowledgebases/{id}` didn't return its `sources` array** — **fixed.** Root cause: `GetKnowledgeBaseByIdQueryHandler` loaded the `KnowledgeBase` via the generic repository's `FindAsync`-based `GetByIdAsync`, which never eager-loads navigation collections, so `Sources` was always the entity's empty default. `IRepository<T>` already had an `Include()` method for this, but it wasn't actually part of the interface contract (dead code, unreachable through DI). Added `IRepository<T>.GetByIdAsync(Guid, Expression<Func<T,object>> include, ...)` (Domain interface + `GenericRepository` implementation, consistent with the existing `Expression`-based methods already on that interface) and used it here: `kbRepo.GetByIdAsync(query.Id, k => k.Sources, cancellationToken)`. **Live-verified**: both the seeded KB and a freshly created one now return their sources correctly; all unit test suites still pass (133/133).
- **Newly-added text/URL sources are never automatically chunked or embedded.** `AddSourceCommandHandler` persists the source row (`Status` stays `null`) but never enqueues an indexing job, and `/reindex` only processes `Documents`, not `Sources`. So RAG retrieval only actually works for content ingested via the file-upload path (which does trigger indexing per the seeded example KB), not the text/URL source path exercised by this pass's testing. This is a functional gap in the RAG pipeline worth a dedicated follow-up — it's a feature-completeness issue, not a quick bug fix.
- **~~Integration test suite returns 500 instead of 401/404 for the large majority of tests, root cause not investigated~~ — correction: found and fixed.** The initial version of this report (written earlier in this same pass) confirmed this was pre-existing and stopped there, since root-causing it looked like it'd require instrumenting the test harness. Continued investigating anyway: it was `RedisCacheService`'s constructor crashing on every request (see finding #14 above). One fix took the suite from 233 failed / 41 passed to 274/274 passing, plus two small test-assertion fixes for legitimate outcomes (429 lockout, 503 fail-closed) the old assertions didn't account for. The CI green/red signal on this project can now actually be trusted.
- **Operational gotcha discovered while standing up the stack, now fixed**: `docker/.env.example` existed but was stale — it referenced `MINIO_ACCESS_KEY`/`MINIO_SECRET_KEY` (not used by the current `docker-compose.yml` at all) and didn't mention `JWT_SECRET`/`ENCRYPTION_KEY`/`AI_PROVIDER`/`OLLAMA_*`/`API_KEY_0`, which the app cannot start without. `docs/deployment/DEPLOYMENT.md`'s docker-compose section didn't mention needing a `.env` file at all — the documented `docker compose up -d` command would fail immediately as written. Rewrote `docker/.env.example` to match the actual compose file, and added an explicit "Configure environment" step to `DEPLOYMENT.md` (including the `DB_PASSWORD` rotation footgun — rotating it after the Postgres volume already exists silently breaks the API with password-authentication failures until the volume is recreated). Did not touch the rest of `DEPLOYMENT.md`, which documents a Kubernetes path referencing `k8s/secret.yaml` etc. that don't exist in this repo (per the README, this is acknowledged as aspirational) — that's a bigger doc/infra decision outside this pass's scope.

---

## 6. Live verification performed

Stood up the full stack via `docker/docker-compose.yml` (Postgres + API + Web) against a real local Ollama instance (`qwen2.5-coder:7b`, matching the compose default). All of the following were driven against the real running app, not mocked:

- **Auth**: login, tampered-JWT rejection (401 via `/auth/me`), brute-force lockout (429 on the 6th failed attempt, matching the configured 5-attempt threshold).
- **AI chat**: created a real conversation, sent a message, received a real model-generated response end-to-end through the retry-wrapped, token-truncation-guarded, kernel-caching-fixed pipeline.
- **RAG**: created a knowledge base, reproduced the source-add 500 on a clean build, fixed it, rebuilt, and confirmed the source persists (see gaps above for what's still incomplete in this pipeline).
- **UI (Playwright, headless Chromium)**: login → dashboard; dark-mode toggle in one session leaving a second fresh session in light mode; dark mode surviving a page refresh; chatbot embed dialog XSS payload rendering fully escaped; admin edit-user dialog loading a single user via the new endpoint without error.
- **Test suite**: full solution (`R2WAI.Api.Tests`, `R2WAI.Domain.Tests`, `R2WAI.Application.Tests`, `R2WAI.Infrastructure.Tests`) run to completion — 407/407 passing.

Not covered live: external integrations (no credentials), workflow/approval end-to-end (time-boxed out after the KB/RAG investigation ran long), multi-browser matrix (only Chromium available here).

---

## 7. Deployment readiness checklist

| Area | Status |
|---|---|
| Critical auth/authz bugs | Fixed and verified |
| Known XSS/SSRF/injection vectors | Fixed (XSS) / accepted risk, documented (SSRF loopback) |
| Brute-force protection | Working, verified live |
| AI reliability (retry, token limits) | Added this pass |
| RAG core path (file upload → embed → retrieve) | Works (per seeded example) |
| RAG text/URL source path | **Broken pipeline gap** — source persists but is never indexed; fix the persistence bug this pass, indexing gap remains |
| Concurrency control on writes | **Not implemented anywhere** — real risk under concurrent edits |
| Test suite health | **Fixed — 407/407 passing** (was 233 failing at the start of this pass; root cause was a Redis resilience bug, not test rot) |
| `.env` / secrets onboarding | **Fixed** — `docker/.env.example` rewritten to match reality, `DEPLOYMENT.md` now documents the required step |
| Missing enterprise features (SSO, billing, compliance) | Unchanged from prior audit — real roadmap items, not bugs |

**Recommendation:** Suitable for an internal single-tenant pilot now, with a trustworthy test suite backing it. Before wider or regulated deployment: decide on and implement concurrency tokens, and close the RAG indexing gap for text/URL sources.
