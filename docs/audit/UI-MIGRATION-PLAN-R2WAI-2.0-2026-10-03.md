# R2WAI 2.0 — UI Migration Plan

- **Date:** 2026-10-03
- **Inputs:** `FRONTEND-AUDIT-R2WAI-2.0-2026-10-03.md`, `BACKEND-AUDIT-R2WAI-2.0-2026-10-03.md`, `docs/adr/0002-primary-navigation-reconciliation.md`, `R2WAI-2.0-Complete-Development-Documentation/14-IMPLEMENTATION-PLAN.md`
- **Status:** Phases 0–2 implemented and verified; Phase 3 shared request/error handling and query-client defaults are implemented and verified. Assistant query keys are centralized; other query-key families, workspace context/switcher, and remaining phases are open.
- **Baseline to preserve:** backend 1,199/1,199 tests (baseline before this frontend-focused slice) · frontend production build exit 0 · frontend Vitest 114/114 · full isolated E2E 43/43 in 7.9 minutes, followed by 11/11 targeted E2E flows after assistant query-key migration. Initial JavaScript chunk is 806.22 kB (244.38 kB gzip), down from 2.59 MB; Vite still reports the shared chunk above its 500 kB advisory threshold. The isolated E2E stack used ports 8180/5150/5433 and its own volumes; containers/network are stopped/removed after the runs, while temporary compose/env files and the test volumes remain.

## 0.1 Verified implementation progress

| Work | Status | Evidence |
|---|---|---|
| Role-aware primary navigation, sidebar groups, mobile shortcuts and route breadcrumb labels consolidated into one typed registry | Implemented | `src/R2WAI.Client/src/lib/nav/roleNav.ts`; `MainLayout.tsx` consumes `getNavSections`, `getBottomNavItems` and `getBreadcrumbs`. |
| Home menu icon and page title source | Implemented | `MainLayout.tsx` derives these from registry metadata. |
| Channel route hierarchy | Implemented | `/deploy` breadcrumbs include the existing Chatbots/Publish parent from registry metadata. |
| Dynamic identifier handling | Implemented | Breadcrumbs suppress only canonical GUID path segments; malformed identifier text remains visible. |
| Navigation tests and production build | Verified | `npm test -- src/lib/nav/roleNav.test.ts`: 27/27; final `npm test`: 104/104; `npm run build`: passed. Route splitting reduced the initial JavaScript chunk from 2.59 MB to 807.68 kB (244.58 kB gzip); a shared-chunk advisory remains. |
| Route render-error recovery | Implemented; build and E2E verified | New `src/R2WAI.Client/src/components/RouteErrorBoundary.tsx`, wired to the router root and auth, protected and main-layout branches. It hides raw exception details, offers reload recovery and shows a correlation ID only when present. |
| End-to-end regression | Verified against final Phase 1 source | `$env:PLAYWRIGHT_BASE_URL='http://localhost:8180'; npx playwright test --workers=1`: 42/42 passed in 5.8 minutes, including live model-backed Copilot and widget checks. |
| Customer-facing URL aliases | Implemented and verified | Replace redirects for `/home`, `/agents`, `/connections`, `/knowledge/new`, `/publish`, and `/activity`, including supported resource IDs. `e2e/compatibility-routes.spec.ts`: 1/1 passed; `/home` preserves query and hash. |
| Shared JSON writes and API error handling | Implemented and verified | Eleven feature API modules delegate POST requests to `postJson`/`fetchJson`; structured ProblemDetails preserves safe detail, correlation ID and field errors. Status guidance now distinguishes 401/403/404/409/422/429/5xx. Unit tests 112/112; full Playwright suite 43/43. |
| Query client defaults | Implemented and verified | `createAppQueryClient` applies 30-second staleness, at most one retry for transient/network and server failures, no retry for 4xx, and no mutation retries. Vitest 114/114; full Playwright suite 43/43. |
| Assistant query keys | Implemented and verified | `queryKeys.assistants` now supplies the existing assistant list/detail/readiness and lookup tuples to queries and invalidations across assistant, home, knowledge, chatbot, monitor, playground and connected-system screens. Unit assertions preserve the previous tuple shapes; affected E2E files pass 11/11. |

**Runner note:** one earlier invocation omitted `PLAYWRIGHT_BASE_URL` and therefore targeted the default `localhost:8080` stack. It reported 5 failures against that older UI/seed state. Those failures occurred before their test actions performed writes; the mutating scenarios that did run completed with their cleanup steps. A subsequent first final-source run found a brittle Home menu assertion that read rendered text instead of accessible labels; that assertion now uses accessible names and route destinations. The 42/42 result above is the isolated 8180 run and is the acceptance result.

**Boundary:** this removes duplicated menu/breadcrumb definitions; it does not make the application’s trusted route graph tenant-editable. The existing `NavigationDefinition` records are bound to a `ConnectedApplication` (`ApplicationId`) and have no global shell-menu endpoint. Reusing that API for product navigation would conflate connector navigation with authorization-sensitive application routes. Runtime-configurable workspace menus remain unimplemented until a dedicated authorized contract exists.

**Still open:** workspace and membership backend, resource scoping/backfill, the phase work below, and full-stack production-like rollout. The repository does not provide evidence about deployed data or migration state. No destructive Department/Application retirement or production migration is authorized by this plan.

---

## 1. The plan in one paragraph

Most of what the brief asks for **already exists or is already satisfied**. The agent journey is free of mandatory Department and Application today (`CreateAssistantCommand` has neither; `CreateEditApplicationDialog`/`CreateApplicationDialog` collect no Department; `/departments` is out of primary nav). The 8-item primary navigation is already shipped in `roleNav.ts`. The real work is therefore **not a redesign** — it is (a) closing four honesty/reliability defects, (b) filling gaps the backend *already supports* but the UI never exposed, (c) unifying vocabulary, and (d) adding compatibility URLs. Workspace selection requires a real backend Workspace and membership contract; no tenant-bound UI stand-in is created. **No screen is built from an invented API.**

## 2. What the brief asks for that this repository cannot honestly deliver

These are stated plainly because the alternative is fabrication.

| Brief target | Blocker | Disposition |
|---|---|---|
| §2 Workspace container, §5 header switcher, §6 full switcher UX, §50-1 "Enter a Workspace" | **No `Workspace` entity, `WorkspaceId`, workspace route, membership model or resolver** — 0 occurrences in client and backend. The repo's own audit plans it as an unstarted 8-phase migration (`BACKEND-AUDIT:67-92`, frontend-compat is phase 6). | **Do not fake it.** Defer the switcher and context until the backend Workspace model, membership, resolver, endpoints and migration/backfill exist. Tenant identity is not relabeled as a Workspace. |
| §13 Execution Trace timeline with real persisted steps | **No durable execution ledger.** `GET /runs` synthesizes assistant rows from chat conversations; status is derived from message presence; errors/latency/tokens/tool-calls/version are `null` (`RunsController.cs:130-150`). | Build the **component** (`ExecutionTrace`) against the one real live source (SSE `onToolCall`/`onCitations` from `POST /assistants/{id}/chat/stream`) and against `WorkflowStepExecution` for automations. Leave historical assistant execution steps explicitly `Not available` until the ledger lands. **Do not render fake steps.** |
| §17 Tool permission Allow/Deny matrix | `ToolDefinition` carries `RiskLevel`, `RequiredRole`, `ConfirmationRequired`, `ApprovalRequired`, `AuditRequired`. **No access-grant field exists.** | Render the fields that exist as a `ToolPermissionTable`: Access derived from `isActive` + `RiskLevel`, Approval from `ApprovalRequired`. Add the Allow/Deny column only when the backend field lands. Server-side enforcement via `ToolGateway` is already correct and must not be duplicated in UI. |
| §31 Global search across Agents/Connections/Knowledge/Automations | **No cross-entity search endpoint.** `CommandPalette` matches nav labels + action commands only. | Extend the palette with the entity list endpoints that **do** exist (`GET /assistants`, `/workspaces`, `/knowledge`, `/automations`, each already paginated + searchable server-side), respecting role nav. Honest, debounced, no new API. |
| §8/§9 per-agent Connections and Tools counts | Agents have no connections/tools collection; `AssistantDto` has one optional `knowledgeBaseId`. | Derive counts from the endpoints that exist (capabilities via `BusinessCapability`, tools via `Capabilities`), or show `Not available`. Never hardcode. |
| §29 model capability verification | No capability metadata verified server-side. | Show model availability + type only. No Chat/Streaming/Tool/Embeddings checkmarks. |
| §24 immutable published versions | `AssistantVersion` + `PublishedAssistantsController` pin correctly, but the **chatbot** path serves the assistant's live config, not the snapshot (`ChatbotDetailPage.tsx:198-215` tooltip). | Surface the pin where it is real (assistant API channel); state the chatbot limitation in the UI as it already does. |
| §3/§38 renaming `/assistants` → `/agents`, `/workspaces` → `/connections`, etc. | Working routes with deep links, bookmarks and 27 e2e specs. ADR-0002 forbids literal implementation of the brief's list. | Add target routes as **redirects** onto the existing paths (the repo already does exactly this for `/applications` → `/workspaces`, `router.tsx:42-45,93-94`). Zero breakage, target URLs work. |

## 3. Sequencing

Each phase ends with `tsc -b` clean, `oxlint` clean, vitest green. Phases 1–6 need **no backend change and no DB migration**.

---

### Phase 0 — Foundations (no user-visible change)
**Goal:** remove the two defects that make every later screen risky.

1. **Route-level error boundary.** Complete. `src/components/RouteErrorBoundary.tsx` is wired on the router root, auth, protected and main-layout branches. It uses the existing `ErrorState`, provides reload recovery, displays a correlation ID only if the error object supplies one, and never renders raw exception details.
2. **Route-level code splitting.** Complete with React Router route-level lazy modules and `useNavigation` loading fallbacks in both layouts. The initial chunk fell from 2.59 MB to 807.68 kB (244.58 kB gzip); Vite still advises further shared-chunk splitting.
3. **Fix `route-smoke.spec.ts`.** Complete: it asserts against `main` (`#main-content`), not `body.innerText()`.
4. **`prefers-reduced-motion`** support. Complete: global theme CSS reduces animation/transition duration and disables smooth scrolling when the OS preference requests reduced motion.
5. Persist sidebar rail-collapse state to `localStorage`. Complete, with reload persistence covered by E2E.

**Verification:** frontend build passed; Vitest 104/104; final isolated Playwright suite 42/42, including route smoke, reload persistence, and reduced-motion checks.

**Files:** new `RouteErrorBoundary.tsx`; `app/router.tsx`, `layouts/MainLayout.tsx`, `theme/theme.ts`, `e2e/route-smoke.spec.ts`.
**Risk:** low. **Verify:** build + lint + vitest; `tsc` must not regress on lazy import types.

---

### Phase 1 — Vocabulary & navigation truth (no backend change)
**Goal:** one word per concept; restore sidebar structure; fix the three false signals.

1. **Nav label ↔ page heading alignment.** Complete: headings now align to `Agents`, `Connections`, and `Publish`; related route and E2E expectations were updated.
2. **Accessible navigation groups.** Complete: each sidebar section is a named `role="group"`; the section’s primary destination and secondary links are generated from the registry and tested by accessible name and route.
3. **`aria-current="page"`, focus and live announcement.** Complete: active sidebar/bottom-nav links expose `aria-current`; route changes focus `#main-content` and announce the page label through a polite live region.
4. **Honest channel configuration feedback.** Complete: save feedback says channel configuration was saved, not that a provider connection was verified.
5. **Remove fabricated platform-health status.** Complete: the static “Platform Healthy / All systems operational” footer was deleted; actual health remains sourced by the AppBar environment chip.
6. **Correct stale runs comment.** Complete: `features/runs/types.ts` describes the current synthesized `/runs` projection and fields it cannot provide.
7. **Verification.** Complete: targeted navigation tests 27/27, Vitest 104/104, production build passed, and isolated E2E 42/42. Home menu assertions use accessible labels and actual route destinations.

**Files:** `lib/nav/roleNav.ts` (unchanged item list — data already correct), `layouts/MainLayout.tsx`, 3 page headings, `components/NotificationBell.tsx` adjacent, `features/runs/types.ts`, e2e specs.
**Explicitly NOT doing:** reordering/flattening nav. ADR-0002 line 60 + `14-IMPLEMENTATION-PLAN.md:108` forbid it, and §3 of the brief permits a compatibility change where repository evidence requires one.

---

### Phase 2 — Compatibility routes; workspace waits for backend support
**Goal:** let users reach the existing supported pages through the brief's customer-facing URLs without implying that Workspaces already exist.

1. **Compatibility redirects.** Complete and verified as replace redirects for `/home`→`/`, `/agents`→`/assistants`, `/agents/:agentId`→`/assistants/:agentId`, `/connections`→`/workspaces`, `/connections/:id`→`/workspaces/:id`, `/knowledge/new`→`/knowledge`, `/publish`→`/chatbots`, `/activity`→`/runs`, and `/activity/:executionId`→`/runs`. `/home` preserves query and hash. `e2e/compatibility-routes.spec.ts`: 1/1 passed.
2. **Workspace switcher and context are deferred.** The backend has no Workspace entity, membership model, resolver, or endpoint. Mapping `tenantId` to a selectable Workspace would expose a misleading domain concept; do not persist a fake selection or send a workspace header. Add the context/switcher only alongside the authorized backend contract and migration/backfill.

**Files:** `app/router.tsx`, `e2e/compatibility-routes.spec.ts`.
**Risk:** low; only URL redirects are added. No authorization or resource scoping changes.

---

### Phase 3 — Reliability & data layer (no backend change)
**Goal:** make every screen that follows correct on failure.

1. **Consolidate local JSON POST implementations.** Complete for the 11 feature modules found in the current code: they now delegate to shared `postJson`/`fetchJson` and retain `authFetch` for existing non-POST calls. API errors now consistently preserve structured backend messages.
2. **Problem-aware `ApiRequestError`.** Complete for the shared JSON path: it carries `correlationId` and field `errors` from problem+json and tolerates `{error}`/`{message}` bodies. `describeApiError()` distinguishes 401/403/404/409/422/429/5xx and includes retry guidance for 429. Unit and E2E coverage pass.
3. **Shared query-key factory** (`src/lib/api/queryKeys.ts`) is partially complete for assistant-related queries and invalidations. Global `QueryClient` defaults are complete in `src/lib/api/queryClient.ts` (`staleTime: 30s`, bounded transient/server retry, no mutation retry). Migrate the remaining feature key families together with their invalidation paths so cache identity remains stable.
4. **Delete the 4 dead components** and the dead `listConversations` import path — or wire the latter in Phase 5.
5. Reduced-motion + focus work from Phase 0 lands here if split.

**Files:** `lib/api/*`, feature `api.ts`, `main.tsx`.
**Risk:** medium — touches every write path. **Verify:** vitest + manual pass over one create/edit/delete per feature.

---

### Phase 4 — Agents (the primary screen)
**Goal:** brief §8–§11 on top of the existing library + studio.

1. **Agents library** (`AssistantsLibraryPage.tsx`): keep the card/table toggle and status counts. Add the **missing Connected System filter** — the backend already accepts `applicationId` on `GET /assistants` (`api.ts:12-24` never sends it; a client-only omission). Present it as *Connections*, not *Application*.
2. **Agent card**: name, `Published · v3` (from `publishStatus` + `publishedVersion` + `publishedAt`), description, Model, Knowledge, Tools. **Connections/Tools counts derived from real endpoints or shown `Not available`** — never invented.
3. **Create Agent** (§9): promote from dialog to `/agents/new`. `AiDraftCreatePrompt` ("What should this agent do?") → `POST /assistants/generate-config` → name + model + capabilities. **No Department, no Application** (already true — this screen simply must not add them).
4. **Save Draft** — `publishStatus: Draft` already exists; surface it as an explicit action rather than an implicit default.
5. **Studio → Agent Detail tabs** (§11): add the three missing tabs over existing endpoints:
   - **Versions** ← `GET /assistants/{id}/versions`, `POST /versions`, `POST /versions/{v}/rollback` (all exist, zero UI today).
   - **Test** ← promote `LivePreviewPane` from an always-on pane to a tab, **plus the missing stop / retry / clear** (Phase 5 mechanics).
   - **Activity** ← `GET /runs?assistantId=` (backend accepts it; `RunsController.cs:44` hardcodes the assistant filter to empty — so **verify server-side before shipping this tab**).
6. Wire `PublishChecklist` into the library's Publish action so preflight is consistent, not Studio-only.

**Files:** `features/assistants/pages/*`, `components/AgentCard.tsx`, `app/router.tsx`, new versions tab.
**Backend dependency:** none new. **DB:** none.
**⚠ Verify first:** `GET /runs?assistantId=` — the controller may return zero rows regardless (`RunsController.cs:44-46`). If so, ship Versions and Test, and mark Activity `Not available` rather than an empty table that looks broken.

---

### Phase 5 — Agent Test Playground (brief §12 — highest UX value)
**Goal:** the ChatGPT-style surface the brief calls "one of the most important screens".

1. **Stop generation**: add `AbortController` to `streamAssistantChat` and abort on stop. The client currently has **zero** `AbortController` usage; `LivePreviewPane` merely disables the composer.
2. **Retry** — re-send the last turn. **Clear conversation** — reset local turns and start a new `conversationId`.
3. **Two-column layout** (conversation | execution trace) on desktop; single column on mobile with the trace in a **bottom sheet** (brief §35).
4. **`ExecutionTrace` component** — feed it the live SSE `onToolCall`/`onCitations` events and `WorkflowStepExecution` for automations. Show duration, sources, tool name, authorization result, errors. **Redact** anything credential-shaped before render (brief §12/§13). Steps with no real backing data render `Not available` — never synthesized.
5. **Conversation history**: wire the already-written `listConversations` (`GET /chat/conversations`) into a session switcher — the endpoint exists; only the call is missing.
6. **Searchable agent picker** — replace the 50-row unsearchable `TextField select`.
7. Progressive status lines during execution (brief §33: understanding → searching knowledge → checking tools → generating) driven by real stream events, not a timer.

**Files:** `features/playground/pages/PlaygroundPage.tsx`, `components/LivePreviewPane.tsx`, `lib/chat/streamAssistantChat.ts`, new `components/ExecutionTrace.tsx`, `features/playground/components/ExecutionInspector.tsx`.
**Security:** never render secrets; redact before display; only show tenant-scoped data the API already returned.

---

### Phase 6 — Connections, Knowledge, Automations, Publish, Activity, Settings
Per area, reuse-first:

| Area | Reuse | Change |
|---|---|---|
| **Connections** | `ApplicationsPage`, `ApplicationWorkspacePage` (669 lines — split), `IntegrationsLibraryPage`, `McpConnectionsPage` | Unify the three lists under one "Connections" heading with type filters (REST/OpenAPI, MCP, Database, Webhook) reflecting **actual** backend types. Give OpenAPI discovery the **two-step review** MCP already has (`DiscoverMcpToolsDialog` is the pattern) — discovery must not equal authorization. Add a connection wizard following the real endpoint order: type → configure → **test** (`POST /integrations/{id}/test`) → discover → review → activate. Secrets never rendered. |
| **Knowledge** | `KnowledgeLibraryPage`, `KnowledgeBaseDetailPage`, `AddKnowledgeDialog` | Add **drag-and-drop**, **per-file retry**, **real progress**. The `setTimeout(400)` "Processing" step must go or be honestly labelled — there is no per-file completion signal. Add chunk count / updated / source to document rows **only where the backend returns them** (currently source-level only). Show `Failed` with the real error. |
| **Automations** | `AutomationBuilderPage` + `builder/` (React Flow, real persistence, real backend step mapping) | **Leave the canvas architecture alone** — it is the one place the repo already satisfies brief §22 properly. Add an AI-first "describe this automation" entry point onto the canvas and a `+ Add Automation` from Home. |
| **Publish** | `PublishChecklist` (server-computed), `DeployChannelsPage`, `WidgetDeploymentPage` | Surface agent publish readiness as the primary Publish screen for agents (not chatbots). Keep backend-provided URLs only. Show channels with their **honest** state. Publish success shows name + version + Live + the real endpoint. |
| **Activity** | `RunsPage`, `RunInspectorDrawer`, `ApprovalsPage` | Fix "View Audit" (no server-side `entityId` filter → silently empty). Add a status-first feed. Show `Not available` for the fields the runs projection genuinely lacks (duration/tokens/model for assistant runs). Approvals: add the **self-approval guard** (`requesterId` vs current user) — currently absent — and fix the page-local sort. |
| **Settings** | `/settings`, `/users`, `/models`, `/security`, `/developer` | Keep task-oriented per brief §28. Surface capabilities on models **only if** the backend provides them (it currently does not). |

**Files:** as listed per row. **Backend dependency:** the only *new* requirement anywhere in this phase is a server-side `entityId` filter for audit logs, which is an existing-endpoint increment, not a new API.

---

### Phase 7 — Visual QA pass
Run brief §49 as a checklist against the implemented screens: spacing, type scale, hierarchy, responsiveness at 3 breakpoints, contrast, empty/loading/error states, interaction feedback, workspace context clarity, absence of unnecessary fields, visual noise. Verify explicitly: **no Department UI, no Application UI, workspace context clear, agent creation ≤ 3 inputs, playground trace legible, publishing unambiguous.**

---

## 4. Cross-cutting requirements

| Brief § | Implementation | Where |
|---|---|---|
| §32 Empty states | `EmptyState` exists and is used per feature; standardize copy + next action | per screen |
| §33 Loading | `LoadingSkeleton` + `aria-busy`; execution-progress lines from real stream events | Phase 5 |
| §34 Errors | `RouteErrorBoundary` + `ErrorState` + correlation id + Try Again; no stack traces | Phases 0, 3 |
| §36 Accessibility | Phase 0/1 a11y work + per-screen labelling; charts need a text alternative | Phases 0, 1, 4–6 |
| §40 UI states | Every touched screen gets initial/loading/empty/success/validation/server/permission/processing/failed/retry | per screen |
| §41 Security | Never client-enforce; map 401/403/404/409/422/429/500; redact secrets; workspace id is a hint only | Phases 2, 3 |
| §42 Performance | Code splitting, pagination (already server-side), debounced search, virtualization for large DataGrids | Phases 0, 4, 6 |
| §43 Real-time | Reuse the 3 existing SignalR hubs; no new protocol | Phase 5 |
| §40 Real data | Where the backend has no data, render `No data yet` / `Not available` — never a placeholder number | all |

## 5. Backend increments this plan would eventually need (not built here)

1. Durable execution ledger + real assistant-run persistence (unblocks brief §13 and `/runs?assistantId=`).
2. `Workspace` entity + membership + workspace-scoped routes (unblocks brief §2/§5/§6 — the repo's own phases 1–6).
3. Tool access-grant field (Allow/Deny), to replace the derived column in brief §17.
4. Knowledge per-document chunk count / updated-at / source, and a per-file completion signal for upload progress.
5. Server-side `entityId` filter on audit logs.
6. Fix the `assistant:{id}` API-key scope so the published-assistant REST channel is reachable by its documented credential (`ApiKeyAuthenticationMiddleware.cs:107-116`).
7. Model capability metadata, if brief §29's verification requirement is to be met truthfully.

## 6. Rollout

Phases 0–3 are behaviour-preserving refactors: ship them independently behind nothing, since they change no contract. Phases 4–6 are screen-level and independently revertable per screen. **No completed phase here requires a DB migration, a backend deploy, or an auth change.** Workspace context and switcher work is explicitly deferred until its backend authorization and data model are ready.

## 7. What this plan deliberately does not do

- No greenfield app, no second UI framework, no replacement of working screens for aesthetic reasons alone.
- No rename of `AssistantDefinition`→`Agent` or `ConnectedApplication`→`Connection` in the database (`14-IMPLEMENTATION-PLAN.md:361`). UI labels only.
- No new nav item implying unbuilt functionality; no nav flattening (ADR-0002).
- No `Department`/`Application` selector anywhere; no deletion of Department/Application data.
- No invented workspace list, execution trace, capability flags, channel state, or URL.
- No client-side authorization. Backend stays authoritative.
