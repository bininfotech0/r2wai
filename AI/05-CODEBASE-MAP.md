# R2WAI 2.0 — Codebase Map

## Status

**POPULATED FROM REPOSITORY** — inspected 2026-10-04.

All paths below were verified against the actual repository. Items that could not be
verified are marked `TBD / VERIFY`.

## Actual top-level structure

```text
src/
├── R2WAI.Api              (69 .cs)   — ASP.NET Core net10.0 host, 26 controllers, middleware, hubs, Elsa workflows
├── R2WAI.Application      (298 .cs)  — MediatR commands/queries, 18 feature slices, Common/{Behaviors,Exceptions,Interfaces,Models,Security,Validation}
├── R2WAI.Domain           (84 .cs)   — Entities(47), Enums(27), Events(3), ValueObjects(1)
├── R2WAI.Infrastructure   (266 .cs)  — EF Core + Npgsql, AI runtime, services, persistence
├── R2WAI.Client           (139 .tsx + 119 .ts) — React 19 + TS + Vite 8 + MUI v9 SPA (not in any .slnx)
├── R2WAI.Widget           (3 .ts)    — embeddable vanilla-TS chat widget (not in any .slnx)
├── R2WAI.slnx             — narrower solution; OMITS tests/R2WAI.Api.Tests (known CI bug)
tests/
├── R2WAI.Api.Tests          (64 .cs)  — controllers, integration, security, middleware
├── R2WAI.Application.Tests  (30 .cs)  — handlers + policy/codec tests
├── R2WAI.Domain.Tests       (29 .cs)  — entity invariants + value objects
├── R2WAI.Infrastructure.Tests (37 .cs) — AI runtime, encryption, egress guard
├── browser-*.mjs (6) + data-seed-fixed.mjs — legacy standalone Playwright scripts
└── screenshots/ (86 .png)
```

Root solution `R2WAI.slnx` = 4 src projects + 4 test projects. This is the solution CI uses
(`.github/workflows/ci.yml`).

## Required audit

### Backend

- **API projects** — `src/R2WAI.Api` (`Microsoft.NET.Sdk.Web`, net10.0), minimal-hosting `src\R2WAI.Api\Program.cs` (595 lines)
- **Controllers/endpoints** — `src\R2WAI.Api\Controllers\` 26 controllers, 273 HTTP action attributes, 75 `[Authorize*]`, 22 `[AllowAnonymous]`. Convention: `[ApiController] [Authorize] [Route("api/v1/[controller]")]`
- **Application services** — `src\R2WAI.Application\Features\` 18 slices (Admin, Applications, Assistants, Auth, BusinessCapabilities, Capabilities, Chat, Chatbots, Documents, Governance, Integrations, KnowledgeBases, Navigation, Operations, Tenants, TestCases, Workflows); each = Commands/ + Queries/ + DTOs/ + Mappings/
- **Commands/queries** — MediatR 12.4 handlers, record + FluentValidation validator co-located in the handler file. Pipeline behaviors: `Application\Common\Behaviors\{ValidationBehavior, AuthorizationBehavior, TransactionBehavior, LoggingBehavior}.cs`
- **Domain entities** — `src\R2WAI.Domain\Entities\` (47), `Enums\` (27), `Events\` (3), `ValueObjects\` (1), `Interfaces\` (2)
- **Domain services** — `TBD / VERIFY` (no dedicated Domain services folder found; domain logic lives in entity methods + application handlers)
- **Infrastructure services** — `src\R2WAI.Infrastructure\Services\` (15) + `BackgroundJobs\`, `ToolFramework\`
- **AI integrations** — `src\R2WAI.Infrastructure\AI\` (17 files) + `AI\Policies\` (11), `AI\ModelGateway\` (8), `AI\DynamicTools\` (6), `AI\Plugins\` (4), `AI\Prompts\`. Semantic Kernel 1.77 + Microsoft.Agents.AI + ModelContextProtocol.Core
- **Authentication** — JWT Bearer (`Program.cs:109-144`, fail-fast secret at `:58-59`, ClockSkew=Zero), refresh tokens hashed at rest (`AuthController.cs:214-217`), API keys (`Middleware\ApiKeyAuthenticationMiddleware.cs`, SHA-256 + `FixedTimeEquals`), optional Entra ID (`Authentication\EntraIdAuthService`), TOTP MFA (`Middleware\MfaSetupScopeMiddleware.cs`)
- **Authorization** — `Filters\TenantAuthorizationFilter.cs`, policies `AdminOnly/TenantAccess/CanManageUsers/CanManageDocuments/CanManageWorkflows` (`Program.cs:146-166`), 3 roles (Admin, User, SystemAdmin) after `CollapseRbacToThreeRoles` migration
- **Policies** — `src\R2WAI.Infrastructure\AI\Policies\` (11 policy classes) + MediatR `AuthorizationBehavior`
- **Audit** — `TBD / VERIFY` audit tables/records not confirmed as a first-class module; audit reporting lives in activity/execution records
- **Approvals** — `TBD / VERIFY` no dedicated approval module found in Application features
- **Background jobs** — `src\R2WAI.Infrastructure\Services\BackgroundJobs\` + hosted services registered in `Program.cs`
- **Workflow infrastructure** — Elsa 3.7 on its own EF/PostgreSQL persistence (`Program.cs:65-103`), `src\R2WAI.Api\Workflows\` (Elsa activities + `NodeProviders\` 10 node types), `Infrastructure\Workflows\`

### Frontend

- **routes** — `src\R2WAI.Client\src\app\router.tsx` (99 lines), `createBrowserRouter`, `lazyPage()` helper for code-splitting, `errorElement: <RouteErrorBoundary/>` on every level
- **pages** — `src\R2WAI.Client\src\features\*\pages\` (36 files)
- **components** — `src\R2WAI.Client\src\components\` (37 shared: PageHeader, EmptyState, ErrorState, FilterBar, LoadingSkeleton, StatusBadge, RouteErrorBoundary, CommandPalette, SchemaForm, UniversalCreate, data/DataTable, charts/, dialogs/)
- **hooks** — colocated `use*` hooks inside feature slices; `lib\voice\useVoice.ts`
- **API clients** — `src\R2WAI.Client\src\lib\api\` (fetchJson, queryClient, queryKeys) + per-feature `features\*\api.ts`
- **state management** — TanStack Query v5 (server state) + `ThemeModeProvider` (UI state); no Redux
- **authentication** — `lib\auth\` (authClient, AuthProvider, RequireAuth, jwt, msal, tokenStorage); SignalR in `lib\signalr\`
- **design system** — MUI v9 + `src\R2WAI.Client\src\theme\{theme.ts, ThemeModeProvider.tsx}`; single component library, no second library
- **tests** — 19 Vitest files (~110 cases, logic-only) + 28 Playwright specs (45 tests) in `src\R2WAI.Client\e2e\`

### Data

- **DbContext** — `src\R2WAI.Infrastructure\Persistence\ApplicationDbContext.cs` + `UnitOfWork.cs` + `GenericRepository.cs`
- **entities** — Domain entities mapped via `Persistence\Configurations\` (44 configurations)
- **configurations** — `src\R2WAI.Infrastructure\Persistence\Configurations\` (44 files)
- **migrations** — `src\R2WAI.Infrastructure\Persistence\Migrations\` (120 files ≈ 60 migrations), latest `20261001192826_…`
- **indexes** — declared inside entity configurations; `TBD / VERIFY` no independent index audit performed
- **global filters** — query filters used (see `AuthController.cs:248-251` comment on `IgnoreQueryFilters()`)
- **seed data** — `Persistence\DatabaseInitializer\` + `DatabaseBootstrap.cs`, invoked at `Program.cs:499` (non-fatal on failure `:501-504`)

### Deployment

- **Dockerfiles** — `docker/Dockerfile.api`, `docker/Dockerfile.client`
- **Docker Compose** — `docker/docker-compose.yml` (services: r2wai-studio nginx:8080, r2wai-api 5000→8080, redis:7-alpine, `pgvector/pgvector:pg16`), plus `.production.yml` and `.monitoring.yml`
- **reverse proxy** — `docker/nginx/studio.conf` (6.3 KB)
- **Kubernetes manifests** — `k8s/` (8 files, kustomized, namespace `r2wai`): namespace, configmap, redis, api-deployment, studio-deployment, ingress, networkpolicy
- **CI/CD** — `.github/workflows/ci.yml` (5 jobs: backend, frontend, e2e, security, docker), `.github/workflows/cd.yml` (GHCR push + `kubectl apply -k` with rollout undo + health smoke test)
- **environment files** — `docker/.env` (gitignored), `docker/.env.example`, `docker/.env.production.example`; override via `__` convention

## Required output

| Module | Actual path | Purpose | Reusable | R2WAI 2.0 status | Notes |
|---|---|---|---|---|---|
| API host | `src/R2WAI.Api` | HTTP endpoints, middleware, hubs, Elsa host | Yes | Existing | 26 controllers, `Program.cs` 595 lines |
| Application layer | `src/R2WAI.Application` | CQRS handlers, validation, policies | Yes | Existing | 18 feature slices, MediatR + FluentValidation |
| Domain | `src/R2WAI.Domain` | Entities, enums, value objects | Yes | Existing | 47 entities |
| Infrastructure | `src/R2WAI.Infrastructure` | EF Core, AI runtime, services | Yes | Existing | 266 .cs, 44 configurations, ~60 migrations |
| AI runtime | `src/R2WAI.Infrastructure/AI` | Model gateway, policies, dynamic tools, MCP | Yes | Partial | No `IToolExecutor`-shaped gateway per `09-API-INTEGRATION-STANDARDS.md:100-112` — `TBD / VERIFY` |
| Connection providers | `src/R2WAI.Infrastructure/Integrations` | External API integrations | Yes | Partial | `IConnectionProvider` / `IConnectionProviderRegistry` interfaces — `TBD / VERIFY` not confirmed |
| Frontend SPA | `src/R2WAI.Client` | React 19 admin app | Yes | Existing | Not referenced by any `.slnx`; own package.json |
| Widget | `src/R2WAI.Widget` | Embeddable chat widget | Yes | Existing | Zero tests |
| Workflows | `src/R2WAI.Api/Workflows` + `Infrastructure/Workflows` | Elsa 3.7 automation | Yes | Existing | Own EF/PostgreSQL persistence |
| Knowledge / RAG | `src/R2WAI.Infrastructure/VectorStore` | pgvector embeddings + retrieval | Yes | Partial | `TBD / VERIFY` ingestion pipeline completeness |
| Backend tests | `tests/*.Tests` | xUnit + Moq + Testcontainers | Yes | Existing | ~938 test methods |
| Frontend tests | `src/R2WAI.Client/{vitest,playwright}.config.ts` | Vitest (19) + Playwright (28) | Yes | Partial | No component render tests; Widget untested |
| Deployment | `docker/`, `k8s/`, `.github/workflows` | Container + k8s + CI/CD | Yes | Existing | Security gates are Critical-only |

## Important

Never fill this document from assumptions. It should be generated or updated after repository inspection.
