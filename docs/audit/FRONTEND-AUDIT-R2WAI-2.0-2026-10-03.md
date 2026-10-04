# R2WAI 2.0 — Frontend Audit (React Client)

- **Date:** 2026-10-03
- **Scope:** `src/R2WAI.Client` (read-only) + backend contract verification in `src/R2WAI.Api` / `Application` / `Domain` / `Infrastructure`
- **Companion:** `BACKEND-AUDIT-R2WAI-2.0-2026-10-03.md`
- **Method:** source inspection only. No file was modified. No database, API or runtime claim was tested against a live stack.
- **Legend:** `VERIFIED` = read in source with `file:line` · `INFERRED` = conclusion drawn from cited code · `UNKNOWN` = not determinable without running the stack.

## 0. Baseline (re-run during this audit, not quoted from docs)

| Check | Command | Result |
|---|---|---|
| Typecheck | `npx tsc -b` | **exit 0**, no output — `VERIFIED` |
| Lint | `npx oxlint` | **exit 0**, 0 errors, 36 warnings (`VERIFIED`) |
| Unit tests | `npx vitest run` | **17 files, 102/102 passed** in 14.6s — `VERIFIED` |
| E2E | `npx playwright test` | **NOT RUN** — requires a running stack (`:8080` API). Last recorded run (`test-results/.last-run.json`, 2026-10-01) is `failed` but the failing assertion has since been rewritten — current status `UNKNOWN` |

Lint warnings are 29 × `react(set-state-in-effect)`, 4 × `react(only-export-components)`, 3 × `react-hooks(exhaustive-deps)`. The `set-state-in-effect` cluster is a cascading-render pattern at ~12 known sites, not a correctness failure.

## 1–8. Stack, routing, state, API, auth, workspace, nav, design system

### 1. Frontend framework — `VERIFIED`
React `19.2.8`, function components + hooks. No class components. No Redux/Zustand/Recoil (`package.json:31`).

### 2. TypeScript version — `VERIFIED`
`~6.0.2` (`package.json:76`). `strict: true`, plus `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`, `noFallthroughCasesInSwitch` (`tsconfig.app.json:11-20`). Path alias `@/*` is configured (`tsconfig.app.json:26-28`) and used **zero** times — all imports are deep relative paths.

### 3. UI framework — `VERIFIED`
MUI `9.3.1` + `@mui/icons-material` `9.3.1` (`package.json:26-27`). Also `@mui/x-data-grid` `9.12.0` (all tables), `@mui/x-date-pickers` `9.12.0`, `@emotion/react`/`styled` `11.14`. No Tailwind, no CSS framework, no component library competing with MUI.

### 4. Build system — `VERIFIED`
Vite `8.2.0` + `@vitejs/plugin-react` `6.0.4`. `build` = `tsc -b && vite build`; there is **no standalone `typecheck` script** (`package.json:63-70`). Dev proxy: `/api` → `localhost:5000`, `/hubs` → `localhost:5000` with `ws:true`, `/api-health` → `/health` (`vite.config.ts:15-30`). API base is therefore always the relative `/api/v1` — **no configurable API host** (`VERIFIED`).

### 5. Routing — `VERIFIED`
`createBrowserRouter` (`src/app/router.tsx:1,47`). **39 route entries**, 3 public + 36 protected. Full table in §A below. All 36 page components are **eagerly imported** (`router.tsx:5-40`) — no `lazy`, no `Suspense`. `RequireAuth` is the only guard (`src/lib/auth/RequireAuth.tsx:5-22`).

Two structural routing facts:
- **No route-level authorization.** `RequireAuth` checks authentication only. `roleNav` is cosmetic by design (`roleNav.ts:4-11`). Any logged-in `User` can navigate to `/users`, `/models`, `/security`, `/tools`, `/developer` by URL; they only don't see links. Backend RBAC is the enforcement point — `INFERRED`.
- **No error boundary anywhere.** No `errorElement` on any route and no error-boundary component exists. A render throw white-screens the app. Precedent is documented in the repo's own e2e header (`e2e/route-smoke.spec.ts:6-13`).

### 6. State management — `VERIFIED`
Server state: `@tanstack/react-query` `5.101.4`. Client state: React Context only — `AuthContext`, `ThemeModeContext`, `SnackbarContext`, `NotificationFeedContext`.

`new QueryClient()` with **zero `defaultOptions`** (`src/main.tsx:17`) — no global `staleTime`/`retry`/`refetchOnWindowFocus`; every hook opts in. Query keys are inline string literals per feature (`['chatbots']`, `['tenant-settings']`, `['assistants','dashboard-breakdown']`) with **no shared key factory** (`VERIFIED`, 192 `useQuery` / 153 `useMutation` / 69 `invalidateQueries` call sites). An invalidation typo therefore fails silently — `INFERRED`.

### 7. API client — `VERIFIED`
`src/lib/api/fetchJson.ts`. One core GET helper plus `authFetch` injection:

```ts
// src/lib/api/fetchJson.ts:31-52
export async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(`/api/v1${path}`, init)
  if (!response.ok) {
    let message = `HTTP ${response.status} for ${path}`
    try {
      const body = await response.clone().json()
      if (body && typeof body === 'object' && typeof (body.message ?? body.error) === 'string') {
        message = body.message ?? body.error
      }
    } catch { /* Non-JSON error body — keep the generic message. */ }
    throw new ApiRequestError(response.status, message)
  }
  return (await response.json()) as T
}
```

Client debt (`VERIFIED`): **12 feature modules define their own local `postJson`** (and `admin`/`applications` also `putJson`). Those bypass `fetchJson`, so structured backend error bodies are lost, failures surface as raw `Error` instead of `ApiRequestError`, and `describeApiError()` (403 → permission copy, `TypeError` → network) protects **reads only**. `ApiRequestError` carries `status` + `message` but **not** the problem+json `code`/`details`, so 400 field errors cannot be mapped to form inputs generically. No timeout/`signal` default, no retry/backoff, no client-side correlation id.

API modules and export counts (`VERIFIED`): `admin` 34 · `applications` 16 · `chatbots` 13 · `knowledge` 13 · `tools` 11 · `assistants` 10 · `automations` 10 · `integrations` 10 · `runs` 10 · `mcp` 9 · `monitor` 19 · `profile` 7 · `capabilities` 6 · `settings` 6 · `approvals` 4 · `departments` 4 · `security` 3 · `lib/notifications` 3.

### 8. Authentication — `VERIFIED`
JWT bearer only. `src/lib/auth/authClient.ts` (264 lines). Tokens in **`sessionStorage`**, keys `r2wai_token` / `r2wai_refresh_token` / `r2wai_user` (`src/lib/auth/tokenStorage.ts:8-47`) — a session dies with the tab, so no XSS-persisted token.

Strengths worth preserving during any refactor:
- **Single-flight refresh**: module-level `refreshInFlight` mirrors the .NET `SemaphoreSlim(1,1)`; concurrent 401s share one refresh (`authClient.ts:188-223`).
- **Deliberate fail-open `/me`**: only 401/403 reject the token; a 500 or network failure returns `{outcome:'trust'}` so an API outage does not mass-logout users (`authClient.ts:128-144`).
- **MFA/password setup tokens are never persisted** — those calls bypass `authFetch`/`tokenStorage` because the token is server-scoped by `MfaSetupScopeMiddleware` (`authClient.ts:44-47`).
- Optional Entra ID via MSAL `5.19.0`, gated on `VITE_ENTRA_CLIENT_ID`/`VITE_ENTRA_TENANT_ID`.

Endpoints: `POST /auth/login|refresh|logout`, `GET /auth/me`, `PUT /auth/profile`, `POST /auth/forgot-password|reset-password|change-password`, `POST /auth/profile/avatar`, `GET /auth/profile/avatar/{userId}`, `POST /auth/request-access|register-member`, `POST /auth/mfa/setup|enable|disable`, `GET /auth/mfa/status`, `POST /auth/entra-id`.

Gaps (`VERIFIED`/`INFERRED`): refresh-rotation failure clears the session with **no user-facing reason** (`authClient.ts:192-223`); no idle timeout or "session ending soon" warning in the client (`UNKNOWN` whether server-side).

### 9. Workspace support — **`VERIFIED: does not exist`**

This is the single most consequential finding and it is negative.

| Probe | Result |
|---|---|
| `workspaceId` in `src/**` | **0 occurrences** (`VERIFIED`, full-tree search) |
| `WorkspaceContext` / `useWorkspace` / workspace reducer/store | none (`VERIFIED`) |
| `features/workspaces/` directory | does not exist (`VERIFIED`) |
| Backend `Workspace` entity, `WorkspaceId`, workspace controller, workspace query filter | **none** in `R2WAI.Api` / `Application` / `Domain` (`VERIFIED`) |
| `/workspaces` route | **means Connections**, not Workspace: `router.tsx:87-88` renders `ApplicationsPage` / `ApplicationWorkspacePage` over `ConnectedApplication` records (`VERIFIED`) |

The frontend holds `UserInfo.tenantId` and reads the `tenant_id` claim, but sends **no tenant identifier in any request path or body** — isolation is entirely server-side (`INFERRED`, enforced by `e2e/cross-tenant-isolation.spec.ts:13,38`).

The repo has already planned Workspace as an **8-phase migration that has not started**: `docs/audit/BACKEND-AUDIT-R2WAI-2.0-2026-10-03.md:67-92` — map dependencies → define semantics → additive schema → backfill → dual-compatible API → **frontend compatibility** → cut over → retire legacy. No phase is complete.

Naming debt already present (`VERIFIED`): the URL says `workspaces`, the breadcrumb says `Connections` (`MainLayout.tsx:81`), the page heading says `Connected Systems` (`features/applications/pages/ApplicationsPage.tsx:70`), and the entity is `ConnectedApplication`. Four names, one destination — and one of those four names is the target concept for something else entirely.

### 10. Existing navigation — `VERIFIED`

`src/lib/nav/roleNav.ts`. Personas: `Public | User | Admin | SuperAdmin`, derived from `SystemAdmin → SuperAdmin`, `Admin → Admin`, else `User` (`roleNav.ts:13-21`). Backend RBAC is genuinely 3 roles (`Program.cs:155-165`); 4 legacy roles were retired by `CollapseRbacToThreeRoles`.

`SuperAdmin` (`roleNav.ts:141`), 7 sections:

| Section | Items (label / path / icon) |
|---|---|
| — | **Home** `/` — hard-coded before all sections (`MainLayout.tsx:467-480`) |
| Agents | Agents `/assistants` · Playground `/playground` |
| Connections | Connections `/workspaces` · Integrations `/integrations` · MCP Servers `/mcp-connections` · Tools & APIs `/tools`¹ · AI Models `/models`¹ |
| Knowledge | Knowledge `/knowledge` |
| Automations | Automations `/automations` |
| Publish | Publish `/chatbots` |
| Activity | Activity `/runs` · Confirmations `/approvals` · Monitor `/monitor` |
| Settings | Settings `/settings` · Users & Roles `/users` · Security & Policies `/security` · API & SDK `/developer` |

¹ SuperAdmin only (`CONNECTIONS_SUPERADMIN`, `roleNav.ts:61-68`). `Admin` drops these two and uses `Users` instead of `Users & Roles` (`:118-125`). `User` gets a flat 5-item set: AI Assistant, Knowledge, My Activity, Notifications, Profile (`:127-136`).

**Against the brief's requested 8-item navigation, the shipped nav already satisfies it** — all eight items are present at top level, in the requested order. See §11 and ADR-0002.

Nav defects (`VERIFIED` / `INFERRED`):
- **Nav label ↔ page heading divergence** in three places: nav `Agents` → page `AI Assistants` (`AssistantsLibraryPage.tsx:197`); nav `Connections` → page `Connected Systems` (`ApplicationsPage.tsx:70`); nav `Publish` → page `Chatbots` (`ChatbotsPage.tsx:90`). The e2e specs assert both vocabularies, so both are load-bearing.
- **`NavSection.label` is now dead UI data** — never rendered (`MainLayout.tsx:482-511` iterates `section.items` only); only `roleNav.test.ts` consumes it.
- Consequence: 18 links render as one flat, unlabeled list. Items like `Integrations`, `MCP Servers`, `AI Models`, `Confirmations`, `Monitor` appear as visually subordinate children of the item above them (`ml: 2`, `0.8125rem`) with no group heading, and a screen-reader user loses the grouping entirely (`INFERRED`).
- `NavItem.icon` is an untyped `string` resolved by `getNavIcon()`; a typo renders `undefined` (`INFERRED`).

### 11. Governance constraint that governs this work — `VERIFIED`

`docs/adr/0002-primary-navigation-reconciliation.md` is **Accepted** and was written specifically about *this* brief:

> "A separately-authored 'Master Prompt R2WAI 2.0' brief specifies a flat 8-item primary navigation…"
> — line 11
> "**`Applications` stays a top-level nav group.** … R2WAI 2.0 already satisfies the brief's actual requirement — no agent, knowledge base, workflow, or tool requires an `ApplicationId` — without needing to hide the entity's own management screen." — lines 39-44
> "Future references to 'the 8-item navigation' from the master brief should be read through this reconciliation, **not implemented literally**." — line 60

`14-IMPLEMENTATION-PLAN.md:108` reinforces it: *"Information architecture decision — NO CHANGE"*, and `:362` — *"Do not drop `Department`, `DepartmentId` or any column. Nullable only, additive migration only."*

The brief's own instruction — *"Use exactly this primary navigation unless repository evidence requires a compatibility change"* — is satisfied by that evidence. **No navigation flattening is required or permitted.**

### 12. Design system — `VERIFIED`

`src/theme/theme.ts` (207 lines), `lightTheme` + `darkTheme`. Dark mode **is** supported and persisted (`ThemeModeProvider` → `localStorage` key `r2wai_dark_mode`).

| Token | Light | Dark |
|---|---|---|
| primary | `#6366F1` (l `#818CF8` / d `#4F46E5`) | `#818CF8` (l `#A5B4FC` / d `#636F1`) |
| secondary | `#7C3AED` | `#A78BFA` |
| success / warning / error | `#16A34A` / `#D97706` / `#DC2626` | `#22C55E` / `#F59E0B` / `#EF4444` |
| background default / paper | `#F8F9FC` / `#FFFFFF` | `#0F1115` / `#171923` |
| divider | `rgba(15,17,21,0.08)` | `rgba(255,255,255,0.08)` |
| `shape.borderRadius` | `10` | `10` |

Font Inter (`@fontsource/inter` 400/500/600/700, `main.tsx:7-10`). Weights set for `h1`–`h6`, `subtitle1/2`, `button` (600, no uppercase), `overline` (700, `0.8` tracking). A purpose-built **25-step shadow scale** (`buildShadows(isDark)`, alpha 0.08 light / 0.45 dark; steps 1-2 `soft`, 3 `raised`, 8 & 24 `overlay`) — this is a real asset, not stock MUI elevation. **17 component overrides**: `CssBaseline` (custom scrollbars), `Button`, `IconButton`, `Paper`, `Card`, `CardActionArea`, `AppBar`, `Tabs`, `Tab`, `Chip`, `TableRow`, `TableCell`, `OutlinedInput`, `ListItemButton`, `Dialog`, `Tooltip`.

Design-system debt (`VERIFIED`):
- **No CSS custom properties** — no `--r2wai-*` tokens anywhere. All color/spacing values are hard-coded hex/rgba literals or palette lookups.
- **The sidebar palette sits entirely outside the theme** — hard-coded `#0B1220` + four rgba constants (`MainLayout.tsx:55-61`), so `divider`/`text` semantics are unavailable there and dark-mode tooling cannot see those values.
- Only 2 CSS files in `src/` (`index.css`, `components/CommandPalette.css`); 117 `sx` breakpoint-key usages bypass any token layer (`INFERRED`).

### 13. Existing reusable components — `VERIFIED`

All requested shared components exist under `src/components/`: `PageHeader`, `EmptyState`, `LoadingSkeleton`, `ErrorState`, `StatusBadge`, `ConfirmDialog` (+ `dialogs/ConfirmDeleteDialog`, `dialogs/FormDialog`), `StatCard`, `FilterBar`, `data/DataTable`, `ChatDialog`, `CopilotPanel`, `PublishChecklist`, `KeyboardShortcutsHelp`, `UniversalCreate`, `AgentCard`, `AiDraftCreatePrompt`, `CodeSnippet`, `MarkdownRenderer`, `NotificationBell`, `ResponseCard`, `SchemaForm`, `Timeline`, `TooltipIconButton`, `VoiceOrb`, `charts/{DonutChart,MultiSeriesChart,TrendLineChart}`.

**4 dead components with zero production references** (`VERIFIED`): `IntegrationCard.tsx:38`, `WizardStepper.tsx:30`, `LoadingSkeleton.tsx:71` (`DetailSkeleton`), `LegacyNotice.tsx:9` (referenced only by its own test).

**Test coverage gap:** only `SchemaForm` and `LegacyNotice` have contract tests. The 14 shared components every feature depends on are untested at unit level.

**Accessibility present** (`VERIFIED`): skip-to-content link with `:focus-visible` reveal (`MainLayout.tsx:219-232`) targeting `<main id="main-content" tabIndex={-1}>`; `aria-label` on 118 icon-only controls; `aria-busy` on loading regions; `aria-expanded`/`aria-haspopup` on the account menu; `aria-label="Breadcrumb navigation"`.

**Accessibility missing** (`VERIFIED`/`INFERRED`): `prefers-reduced-motion` — **0 occurrences**, transitions are unconditional; no route-change focus management (nothing focuses `#main-content` after navigation); no live regions; no `aria-current="page"` on active nav; charts have no text alternative; sidebar group headings absent (§10); muted sidebar text `rgba(255,255,255,0.45)` on `#0B1220` computes to ≈4.0:1, below AA at `0.8125rem` (`INFERRED`).

### 14. Tests — `VERIFIED`

- **Unit:** 17 files / 102 tests, vitest `4.1.11`, jsdom, `src/test/`. All pass.
- **E2E:** 27 spec files, Playwright `1.62.1`, Chromium only, `baseURL http://localhost:8080`, `fullyParallel`. Real backend round-trips (create/edit/delete per feature), plus `cross-tenant-isolation.spec.ts` (foreign-tenant id → 404) and `route-smoke.spec.ts`. **Not run** — needs the container stack. Hard-coded seeded credentials in `e2e/fixtures/auth.ts` (`VERIFIED`).
- **`route-smoke.spec.ts` is materially weakened** (`INFERRED`): it asserts against `page.locator('body').innerText()` (`:66`), which **includes the always-present sidebar**. `/tools` expects `/Tools/i` — satisfied by the sidebar's `Tools & APIs`; `/approvals` by `Confirmations`; `/monitor` by `Monitor`; `/knowledge` by `Knowledge`; `/` by the `Home` nav link alone. A crashed page can still pass. It should be scoped to `main`.
- README is still the stock Vite scaffold — no architecture, auth or run documentation (`VERIFIED`).

## A. Full route table — `VERIFIED` (`src/app/router.tsx`)

**Public** (`AuthLayout`): `/login` · `/forgot-password` · `/reset-password`

**Protected** (`RequireAuth` → `MainLayout`):

| Path | Component |
|---|---|
| `/` | `HomePage` |
| `/departments` | `DepartmentsPage` |
| `/assistants` | `AssistantsLibraryPage` |
| `/assistants/:id` | `AssistantStudioPage` |
| `/playground` | `PlaygroundPage` |
| `/automations` | `AutomationsListPage` |
| `/automations/:id` | `AutomationDetailPage` |
| `/automations/:id/builder` | `AutomationBuilderPage` |
| `/knowledge` | `KnowledgeLibraryPage` |
| `/knowledge/:id` | `KnowledgeBaseDetailPage` |
| `/integrations` | `IntegrationsLibraryPage` |
| `/mcp-connections` | `McpConnectionsPage` |
| `/chatbots` | `ChatbotsPage` |
| `/chatbots/:id` | `ChatbotDetailPage` |
| `/deploy` | `DeployChannelsPage` |
| `/deploy/widget` · `/deploy/widget/:chatbotId` | `WidgetDeploymentPage` |
| `/deploy/whatsapp/:chatbotId` | `WhatsAppSetupPage` |
| `/developer` | `DeveloperPage` |
| `/workspaces` · `/workspaces/:id` | `ApplicationsPage` · `ApplicationWorkspacePage` |
| `/applications` · `/applications/:id` | redirects → `/workspaces[/:id]` (compat, `:42-45`, `:93-94`) |
| `/runs` | `RunsPage` |
| `/approvals` | `ApprovalsPage` |
| `/monitor` | `MonitorPage` |
| `/users` · `/models` · `/security` · `/settings` | `UsersPage` · `ModelsPage` · `SecurityPolicyCenterPage` · `SettingsPage` |
| `/tools` · `/tools/:id` | `ToolsListPage` · `ToolDetailPage` |
| `/profile` · `/inbox` · `/about` | `ProfilePage` · `InboxPage` · `AboutPage` |
| `*` | `NotFoundPage` |

## B. Per-area screen audit

### Agents — `features/assistants/` — `VERIFIED`
- **Library** (`AssistantsLibraryPage.tsx`, 359 lines): card grid **and** DataTable toggle; server-side search, status filter (`Draft/Published/Archived`) **with counts**, sort (`recent/name/usage/least-used`). Card actions `Open / Publish|Unpublish / Duplicate`. **No Connected System filter** even though its own comment claims status counts are scoped by `applicationId` (`:51-52`) and the backend supports that param — a client-only omission, not a backend gap.
- **Studio** (`AssistantStudioPage.tsx`, 530 lines) tabs: `Overview · Knowledge · Capabilities · Behavior · Security · Channels · Analytics`. **No Test tab** (live test is the always-on `LivePreviewPane`), **no Versions tab** (backend has `GET/POST /assistants/{id}/versions` + `rollback`), **no per-agent Activity tab**.
- **Create** (`CreateAssistantDialog.tsx`): AI-first via `AiDraftCreatePrompt` → `POST /assistants/generate-config`, or manual **Name / Type / Base Model** only. **Confirms no Department field and no Application selector.**
- **`AssistantDto`** (`types.ts:21-40`): `id, applicationId, name, description, type, systemPrompt, modelConfigurationId, knowledgeBaseId, tools, settings, isActive, publishStatus, publishedVersion, publishedAt, tags, avatarUrl, usageCount, createdAt`. Note: there is **no `instructions` field** — the Studio labels `systemPrompt` as "Instructions". `tools`/`settings` are **JSON-encoded strings**, not typed.
- **Publish readiness** (`GET /assistants/{id}/readiness` → `{checks:[{label,ready,detail}]}`) is surfaced only in the Studio; the library's Publish action is a bare `ConfirmDialog` with no preflight.

### Connections — `features/applications/` + `integrations/` + `mcp/` + `tools/` — `VERIFIED`
**There is no unified connection entity — there are three parallel lists plus a tool registry:**

| Surface | Route | Backend model |
|---|---|---|
| Connections | `/workspaces` | `ConnectedApplication` |
| Integrations | `/integrations` | `ToolDefinition`, `lastTestStatus: Connected\|Error\|null` |
| MCP Servers | `/mcp-connections` | `McpServerConnection` |
| Tools & APIs | `/tools` | `ToolDefinition` / Capability |

- `ApplicationWorkspacePage.tsx` is **669 lines** with tabs `General · APIs · Configuration · Versions · Ecosystem`.
- `CreateApplicationDialog.tsx` collects Name, Code, Description, Base URL, Environment — **no Department selector** (`features/applications/types.ts:46-47` documents that it is deliberately absent and the backend still rejects an out-of-tenant id).
- **Discovery review:** MCP has a real two-step flow — analyze → selectable tools → commit, and committed tools land **inactive** so nothing is agent-callable until reviewed. Application OpenAPI discovery (`DiscoverApplicationDialog.tsx`, 101 lines) is **one-step**: URL/Upload → single *Discover* → it auto-creates one governed capability per discovered operation. **No operation list, no checkbox selection, no pre-commit review.**
- **Tool permissions** (`CreateEditCapabilityDialog.tsx:97-120`) expose Risk level, Required role, Approval required, Confirmation required, Audit every call. **There is no Allow/Deny access model** — it is a role-*gate*, not a role-*grant* (`features/tools/types.ts` has no such structure). Target §17's Allow/Deny table has **no backend field to bind to**.

### Knowledge — `features/knowledge/` — `VERIFIED`
`KnowledgeBaseDetailPage.tsx` (515 lines) tabs `Overview · Sources · Documents · Retrieval · Security · Usage`. Document rows show name, fileType, fileSize, status, actions — **no chunk count, no upload date, no source link** (chunk data exists at *source* level only). Statuses in use: `Active, Creating, Failed, Ready, Processing, Uploading`.

`AddKnowledgeDialog.tsx` (293 lines) defects: **no drag-and-drop** (hidden `<input type=file multiple>`); **no per-file retry**; `LinearProgress` with **no percentage**; a **simulated "Processing" step** implemented as `setTimeout(400)` because there is no per-file completion signal (`:96-99`, honestly commented); strictly sequential uploads. "Used By" is derived client-side from `listAssistants(1,200,'')` because no endpoint reports KB usage (`:98-105`).

### Automations — `features/automations/` — `VERIFIED` (strongest area)
`AutomationBuilderPage.tsx` (351 lines) uses `@xyflow/react` and is **real, not decorative**: `useNodesState`/`useEdgesState`/`addEdge`, custom `nodeTypes`, drag-connect editing, persisted via `updateWorkflow`, round-tripped by `builder/graphConversion.ts` (node↔step mapping, positions stashed in `config._canvasPosition`, unit-tested).

Node palette maps 1:1 to the backend `StepType` union (`Action, Approval, AI Generate, Email, API Call, Delay, Condition, Transform`) — `AI→AI Generate`, `Decision→Condition`, `Tool/API→API Call`, `Notify→Email`, `Approval→Approval`, `Wait→Delay`; Start/End are structural. `AutomationDetailPage.tsx` (334 lines) has a dynamic step editor + `PublishChecklist`.

Gap: no "describe the automation" entry *on the canvas* (it exists only in the create dialog), and no branch preview beyond React Flow edges.

### Playground — `features/playground/` — `VERIFIED` (biggest UX gap vs brief §12)
Three panels: mode tabs (`Assistant | Automation | Tool/API`) + instance picker, center live execution, right `ExecutionInspector`.

- **Streaming exists:** `streamAssistantChat` → `POST /assistants/{id}/chat/stream` (SSE) with `onChunk / onCitations / onToolCall / onContentBlocks / onDone / onError` (`src/lib/chat/streamAssistantChat.ts:16-35`), falling back to `chatWithAssistant`.
- **No stop generation. No retry. No clear conversation.** A repo-wide search for `AbortController|abort()|StopIcon|onStop` returns **zero matches** (`VERIFIED`). `LivePreviewPane` merely disables the composer while streaming.
- **No conversation library.** `listConversations` (`features/runs/api.ts:50-53`) is exported and **never called** — dead code.
- **Trace is live-only.** `LivePreviewPane` shows a tool-call `Timeline` and `Sources: [n] sourceName` for the *current turn only*. `ExecutionInspector` mirrors the live session. `RunInspectorDrawer` (historical) shows **no tool calls** — it renders `—` (`:200-207`), honestly noted at `:204-206`. **There is no persisted execution trace, because the backend has none** (see §C).
- Picker is an unsearchable `TextField select` capped at 50 rows — beyond that, agents are unreachable.

### Publish — `features/chatbots/` + `components/PublishChecklist.tsx` + `features/deploy/` — `VERIFIED`
- Nav `Publish` → `/chatbots` (`roleNav.ts:87-90`).
- **Assistant publishing is genuinely solid and server-authoritative**: `GET /assistants/{id}/readiness` computes checks server-side and `POST /assistants/{id}/publish` **hard-fails 409** on any unmet check (`AssistantsController.cs:170-179`). `PublishChecklist` is reused in the Studio and `AutomationDetailPage:340`.
- **Chatbot "publish" bypasses all of it** — the button is just `setChatbotStatus('Active')` (`ChatbotDetailPage.tsx:218-226`), no readiness preflight.
- **Versions are not immutable.** The `publishedAssistantVersionNumber` chip's own tooltip states it is "a record only — the chatbot still serves the assistant's current live configuration, not a version pinned to this snapshot" (`:198-215`).
- **Channels are not honest.** `ChatbotDetailPage.tsx:134-141` announces `"${channel} channel connected"` on a stored-empty payload, while `DeployChannelsPage.tsx:32-36` **explicitly refuses** to show green "Connected" for messaging channels because no provider adapter exists. Two screens assert opposite truths about the same state.
- Widget URLs are **real** — persisted via `PUT /chatbots/{id}/widget`, embed verification reads `widgetLastSeenAt/Origin`. **No fabricated URLs** (`VERIFIED`).
- Telegram is deliberately absent everywhere — no enum member, adapter or webhook.

### Activity — `features/runs/` + `features/approvals/` + `features/monitor/` — `VERIFIED`
`RunsPage.tsx` (162 lines): `DataTable`, page size 25, optional 10s live polling switch, debounced search, filters (type, status, application, assistant, user, started-range). Row click → `RunInspectorDrawer`. APIs: `GET /runs`, `GET /runs/filters`, `GET /workflows/instances[/{id}[/steps]]`, `POST /workflows/instances/{id}/retry`, `GET /chat/conversations[/{id}[/messages]]`, `GET /operations/audit-logs`.

Defects:
- `runs/types.ts:93-101` claims "no backend endpoint returns this combined shape … built as a client-side merge" — **stale and contradicted by the live call**, which passes `type`/`applicationId`/`assistantId`/`userId` to a real unified `GET /runs`. Misleading to the next engineer (`VERIFIED`).
- "View Audit" has **no server-side `entityId` filter**, so it filters the current 25-row page client-side and can deep-link to an empty table (`runs/api.ts:64-67`, `AuditLogsTab.tsx:33-35,100`).
- Automation retry takes only an instance id — the drawer offers one "Retry" for any failed step with no step targeting (`RunInspectorDrawer.tsx:79-89,134-145`).
- **Approvals** (`ApprovalsPage.tsx`, 298 lines) tabs Pending/Approved/Rejected; card shows subject, requester, timestamps, app chip, up to 4 parsed data pairs, Approve/Reject/View Details. **No self-approval prevention in the UI** — `requesterId` is in the DTO but never compared to the signed-in user. Backend enforcement `UNKNOWN`. Header promises "sorted by escalation and due date" but sorting is **page-local** after a `pageSize:50` fetch. History rows come from the raw entity with no joins, so titles degrade to `Workflow {id.slice(0,8)}…`.
- **Monitor** (`/monitor`) is deliberately distinct from Runs: platform health (Overview, Errors, AI Operations, Audit Logs, Usage Analytics) + a separate QA & Testing menu (Reports, Test Cases, Test History). Its limitations are **honestly disclosed in-UI**: Errors parses server log files not request records; token counts read 0 because the chat write path never records per-message usage; Overview health counts are computed client-side from 200-row bounded fetches.

### Settings — `VERIFIED`
`/settings` (tenant settings), `/users` (users + roles), `/models` (AI model registry), `/security` (policy center), `/developer` (API keys), `/profile`, `/inbox`. Backend: `AdminController` settings/roles/models/audit-logs/content-moderation, `UserManagementController` users, `ApiKeysController`, `GovernanceController`, `ApprovalsController/policies`.

Models expose no capability flags to verify Chat/Streaming/Tool Calling/Embeddings against — brief §29's "only display capabilities actually verified" is **not satisfiable today** (`UNKNOWN` whether capability data exists server-side).

## C. Backend contract verification for the target screens — `VERIFIED`

Full backend inventory is in `BACKEND-AUDIT-R2WAI-2.0-2026-10-03.md`. Findings that directly gate target screens:

| Brief target | Backend reality |
|---|---|
| §2 Workspace container | **No Workspace entity/Id/filter/controller.** Migration planned in 8 phases, none started. |
| §5–6 Workspace switcher | Not implementable without inventing APIs. |
| §8 Agent `Connections: 6` / `Tools: 8` / `Knowledge: 12 sources` | Agents have **no** connections or tools collection. `AssistantDto` has one optional `knowledgeBaseId`; tools come via `Capabilities` and `BusinessCapability`. Counts must be derived or shown as unavailable. |
| §9 Create Agent | **Already satisfied.** `CreateAssistantCommand(Name, Type, ModelConfigurationId?, KnowledgeBaseId?)` — no Department, no Application. Department was made nullable in Phase 1 (`ConnectedApplication.cs:15-20`), with the cross-tenant check retained (`CreateApplicationCommand.cs:45-48`). |
| §13 Execution Trace timeline | **No durable execution ledger.** `GET /runs` synthesizes assistant rows from chat conversations with **derived** status (message presence) and `null` for errors, latency, tokens, model, tool calls and version (`RunsController.cs:130-150`); correlation id is back-derived from `AuditLogs` (`:126-127`). Two filters hardcode to empty (`:41-46`). |
| §16 Discovery ≠ authorization | Real for MCP (commit lands inactive). **Not** for OpenAPI-on-Application (one-step, auto-creates capabilities). |
| §17 Tool Allow/Deny | `ToolDefinition` has RiskLevel, RequiredRole, ConfirmationRequired, ApprovalRequired, AuditRequired — **no access-grant field**. |
| §24 Immutability | `AssistantVersion` exists and `PublishedAssistantsController` pins to the snapshot — but the chatbot path does not pin (see above). |
| §31 Global search across entities | **No search endpoint spans Agents/Connections/Knowledge/Automations.** `CommandPalette` matches nav labels + action names only. Its topbar placeholder promises "Search assistants, knowledge, tools…" — a promise the palette cannot keep (`MainLayout.tsx:304`). |
| §41 error contract | Global `ExceptionHandlingMiddleware` emits `application/problem+json` `{status,title,detail,type,correlationId[,errors]}` (`ExceptionHandlingMiddleware.cs:158-180`), but **many controllers return ad-hoc `{ error }`** (`PublishedAssistantsController.cs:61`, `ChatbotsController.cs:500`, `AssistantsController.cs:161`). Clients must handle both — as `fetchJson` already does. |
| §41 pagination | **Inconsistent**: `{items,totalCount,page,pageSize}` in Runs/Notifications vs `{items,total,page,pageSize}` in admin webhooks. No shared contract. |
| §43 Real-time | 3 SignalR hubs (`/hubs/chat`, `/hubs/status`, `/hubs/notification`); `StatusHub` emits `WorkflowStepStarted/Completed/Failed`, `WorkflowCompleted/Failed`, wired via Elsa notification handlers. Reuse — do not invent. |
| §30 Roles | Genuinely 3: `Admin`, `User`, `SystemAdmin` (`Program.cs:155-165`). Brief's SUPER ADMIN/ADMIN/USER maps cleanly. |

**Security posture to preserve:** tenant isolation is fail-closed in three layers — `TenantResolutionMiddleware` (403 without a tenant), `TenantAuthorizationFilter` (cross-tenant → `UnauthorizedException`), and a reflection-applied EF query filter (`ApplicationDbContext.cs:315-330`). The frontend must not weaken this and must not add a client-supplied tenant/workspace id that the backend treats as authoritative.

## D. Department / Application reference inventory

`departmentId` / `DepartmentId` in `src/**` — 4 non-test sites only:
- `features/admin/types.ts:103,129` — `departmentId: string | null` on a user DTO
- `features/applications/types.ts:31,46-47` — `departmentId: string | null` on the connection DTO, with a comment documenting deliberate absence from the create form
- `features/departments/**` — the standalone `DepartmentsPage` + dialog (out of primary nav; a test asserts `/departments` stays out, `roleNav.test.ts:210-213`)

`applicationId` in `src/**` — read/filter plumbing on DTOs (`assistants`, `automations`, `tools`, `knowledge`, `approvals`, `monitor`, `runs`, `admin`) and query params. **No create form collects it**; `AssistantDto.applicationId` is populated by the separate `POST /assistants/{id}/application` assign action.

**Conclusion (`VERIFIED`): the brief's core requirement — no mandatory Department or Application in the agent journey — is already satisfied in code and in UI.** `CreateAssistantDialog` collects neither. `CreateApplicationDialog` has no Department selector. `/departments` is deliberately out of primary nav. This is Phase 1, shipped 2026-10-01.

## E. Consolidated problem list (ranked)

| # | Sev | Problem | Class |
|---|---|---|---|
| 1 | High | **No error boundary anywhere** — any render throw white-screens the app | `VERIFIED` |
| 2 | High | **No stop / retry / clear generation** in any chat surface; zero `AbortController` client-wide | `VERIFIED` |
| 3 | High | **Chatbot "publish" has no readiness gate** and its version chip is not a version pin | `VERIFIED` |
| 4 | High | **Channels assert contradictory truth** — "connected" in chatbot detail vs "unverified" on deploy page | `VERIFIED` |
| 5 | High | **No workspace concept anywhere**; `/workspaces` URL means Connections | `VERIFIED` |
| 6 | Med | **No route-level authorization** — admin URLs reachable by any authenticated user | `INFERRED` |
| 7 | Med | **No code splitting** — one 2.59 MB chunk, 36 eager page imports | `VERIFIED` |
| 8 | Med | **12 modules re-implement `postJson`**, bypassing error normalization; reads-only `describeApiError` | `VERIFIED` |
| 9 | Med | **OpenAPI discovery has no operation review step** (MCP does) | `VERIFIED` |
| 10 | Med | **No tool Allow/Deny model** — role gate only, no backend field | `VERIFIED` |
| 11 | Med | **Knowledge upload**: no drag-drop, no retry, indeterminate progress, simulated processing | `VERIFIED` |
| 12 | Med | **No assistant Versions / Activity / Test tab** despite backend support for versions | `VERIFIED` |
| 13 | Med | **No persisted execution trace** — backend has no ledger; assistant runs are synthesized | `VERIFIED` |
| 14 | Med | **Nav label ↔ page heading split** in 3 places; 18 links render unlabeled | `VERIFIED` |
| 15 | Med | **"View Audit" filters client-side on page 1** → silently empty table | `VERIFIED` |
| 16 | Med | 29 `set-state-in-effect` warnings (cascading renders) | `VERIFIED` |
| 17 | Low | **No `prefers-reduced-motion`** support (0 occurrences) | `VERIFIED` |
| 18 | Low | No route-change focus management, no live regions, no `aria-current="page"` | `VERIFIED` |
| 19 | Low | Query keys are inline literals, no factory, no global QueryClient defaults | `VERIFIED` |
| 20 | Low | 4 dead components; `listConversations` dead; stale comment at `runs/types.ts:93-101` | `VERIFIED` |
| 21 | Low | `route-smoke.spec.ts` asserts on `body.innerText()` incl. sidebar → can pass on chrome | `INFERRED` |
| 22 | Low | Static "Platform Healthy / All systems operational" string, not a health check | `VERIFIED` |
| 23 | Low | Command-palette placeholder over-promises entity search | `INFERRED` |
| 24 | Low | Sidebar palette outside theme; AA contrast risk at `0.8125rem` | `VERIFIED`/`INFERRED` |
| 25 | Low | `README.md` is the stock Vite scaffold | `VERIFIED` |

## F. What is genuinely strong and must not be broken

The codebase is **unusually honest**: nearly every unavailable thing is a visible `—`, an explicit error state, or a documented backend gap rather than a fake. Specifically —

- Tenant isolation fail-closed in three layers; no client-supplied tenant id.
- Publish readiness computed **server-side** and enforced with 409; the UI reflects it.
- MCP discovery commits tools **inactive**, so discovery ≠ authorization.
- `ToolGateway.InvokeAsync` is the single enforcement point on both the Semantic Kernel and Microsoft Agent Framework runtimes (`ToolGateway.cs:29,63`; `MafToolFunctionFactory.cs:79-84`), and `IntegrationsController.Test` no longer bypasses it.
- Widget/channel URLs come from the backend; Telegram is absent rather than faked.
- Monitor discloses its own data limits in the UI.
- `departmentId` is optional **and still tenant-validated** — "optional is not unvalidated".

This is the property the redesign must preserve above all: **the UI never claims more than the backend can prove.**

## G. Unknowns

1. E2E pass/fail against a live stack — not run.
2. Whether the backend blocks an agent from approving its own operation (`UNKNOWN`; no client check, backend not verified).
3. Whether `/runs` is scoped to the caller for the plain-`User` persona, making the label "My Activity" an over-promise.
4. Whether AI models carry any capability metadata server-side (blocks brief §29).
5. Whether `Document` read is intentionally `Admin|SystemAdmin`-only — a plain `User` cannot read a document they uploaded.
6. `NotificationHub` client method surface — hub is mapped, methods not read.
7. Live DB migration level, tenant data counts, real dependency on existing Department/Application records.
8. `ADMIN` role semantics on `OperationsController.GetErrors` / audit export — tenant admins appear able to read audit export.
9. Whether `PublishedAssistantsController`'s documented `assistant:{id}` API-key scope can authenticate at all — the scope middleware requires `read`/`write`, which `assistant:{id}` does not satisfy (`ApiKeyAuthenticationMiddleware.cs:107-116`). A functional break in the published-assistant API channel.