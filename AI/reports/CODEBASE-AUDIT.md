# R2WAI Codebase Audit

## Status

`VERIFIED 2026-10-04`

Populated from the actual repository by direct inspection.

## Repository

`C:\Users\LENOVO\Bots\R2WAI`

## Audit goals

Determine:
- actual architecture
- actual project structure
- implemented modules
- current database model
- current API surface
- current AI runtime
- current workflow runtime
- current knowledge pipeline
- current integrations
- current security
- current frontend
- current deployment
- current tests

## Audit rules

Do not infer implementation from old documentation.

## Findings

### Projects

Polyglot monorepo. Root `R2WAI.slnx` references 4 src projects (`R2WAI.Api`,
`R2WAI.Application`, `R2WAI.Domain`, `R2WAI.Infrastructure`) and 4 test projects.
`Directory.Build.props`: `net10.0`, `ImplicitUsings`, `Nullable`, `TreatWarningsAsErrors=false`,
`EnforceCodeStyleInBuild=true`; no central package management, no `global.json`.

`src/R2WAI.slnx` is a second, narrower solution that omits `tests/R2WAI.Api.Tests` — a known
past CI incident documented at `.github/workflows/ci.yml:29-33` and `:158-161`.

`src/R2WAI.Client` (React SPA) and `src/R2WAI.Widget` are not referenced by either `.slnx`;
they build via their own `package.json` scripts.

Root `package.json` has **no `scripts` block** — there is no single repo-root command to
install/build/test everything.

### Backend

ASP.NET Core, C# 13, net10.0, minimal-hosting `src\R2WAI.Api\Program.cs` (595 lines) with
attribute-routed MVC controllers.

- 26 controllers in `src\R2WAI.Api\Controllers\`, 273 HTTP action attributes, 75 `[Authorize*]`,
  22 `[AllowAnonymous]`. Dominant pattern: thin controller → `IMediator.Send`
  (e.g. `DepartmentsController.cs:17-23`).
- Non-controller endpoints: SignalR hubs `/hubs/chat|status|notification` (`Program.cs:423-425`),
  health `/health|/health/startup|/health/ready` (`:426-483`), Prometheus `/metrics/prometheus` (`:488`).
- Secure-by-default: `Program.cs:422` `app.MapControllers().RequireAuthorization();`.
- Error handling: single central `Middleware\ExceptionHandlingMiddleware.cs` (185 lines,
  registered `Program.cs:391`) mapping typed exceptions → RFC-7807-ish `application/problem+json`
  with `correlationId` + `X-Correlation-Id` header. Custom exceptions in
  `Application\Common\Exceptions\`.
- MediatR pipeline: `ValidationBehavior`, `AuthorizationBehavior`, `TransactionBehavior`,
  `LoggingBehavior` in `Application\Common\Behaviors\`.
- **Deviation:** `AuthController.cs:21` injects `ApplicationDbContext` directly and does LINQ in
  the controller (796 lines); `Middleware\ApiKeyAuthenticationMiddleware.cs:57` also resolves
  `ApplicationDbContext`. No `IApplicationDbContext` abstraction exists in the Application layer.
  Conflicts with `06-BACKEND-STANDARDS.md:71-73` ("Do not access the database directly from
  controllers").

### Frontend

React 19.2 + TypeScript ~6.0 + Vite 8 + MUI v9 + react-router-dom v7 data router +
TanStack Query v5 + react-hook-form/zod + @xyflow/react (workflow canvas) + @microsoft/signalr.

- 227 files under `src\R2WAI.Client\src`; 26 feature slices, each `api.ts` + `types.ts` +
  `pages/` + optional `components/`/`dialogs/`.
- Consistent conventions: named function exports for pages, per-feature `QUERY_KEY` +
  `invalidateQueries`, `lazyPage()` code-splitting, `RouteErrorBoundary` on every route level.
- RBAC-driven nav in `lib/nav/roleNav.ts`.
- Lint = oxlint with only 2 rules enabled (`react/rules-of-hooks: error`,
  `react/only-export-components: warn`); no ESLint/Prettier. Typecheck happens only as part of
  `npm run build` (`tsc -b`).

### Database

EF Core 10 + Npgsql PostgreSQL (pgvector enabled, `VectorStore:Provider=pgvector`).

- `Persistence\ApplicationDbContext.cs`, `UnitOfWork.cs`, `GenericRepository.cs`.
- 44 entity configurations in `Persistence\Configurations\`.
- ~60 migrations (120 files incl. designers) in `Persistence\Migrations\`, latest
  `20261001192826_…`.
- Handlers use repository + unit-of-work (e.g. `Features\Applications\Commands\CreateDepartmentCommand.cs:23-42`),
  not DbContext directly — except the two backend deviations listed above.
- Bootstrap: `DatabaseInitializer.InitializeAsync` + `DatabaseBootstrap.cs` at `Program.cs:499`,
  non-fatal on DB failure (`:501-504`).
- Startup config gate: `Program.cs:536-593 ValidateProductionConfig` throws in Production for
  missing/placeholder secrets.

### AI

`src\R2WAI.Infrastructure\AI\` (17 files) plus subfolders `Policies\` (11),
`ModelGateway\` (8), `DynamicTools\` (6), `Plugins\` (4), `Prompts\`. Packages: Semantic Kernel
1.77, Microsoft.Agents.AI, ModelContextProtocol.Core. Vector store: pgvector.
`src\R2WAI.Api\Workflows\NodeProviders\` exposes 10 workflow node types.

`TBD / VERIFY`: whether the tool gateway exposes the controlled abstractions named in
`09-API-INTEGRATION-STANDARDS.md:100-112` (`IToolRegistry`, `IToolExecutor`,
`IOperationSchemaProvider`) under those exact names.

### Integrations

`src\R2WAI.Infrastructure\Integrations\` + `Storage\` (Local/MinIO) + `Cache\` (StackExchange
Redis) + optional Entra ID auth. OpenAPI ingestion driven by Swashbuckle-generated schema
(`Program.cs:396-404`, Development/Staging only — no published OpenAPI artifact).

`TBD / VERIFY`: presence of `IConnectionProvider` / `IConnectionProviderRegistry` named
abstractions required by `09-API-INTEGRATION-STANDARDS.md:100-112`.

### Security

- Pipeline: JWT Bearer (fail-fast secret, ClockSkew=Zero) → `TenantResolutionMiddleware` →
  `TenantAuthorizationFilter` (global MVC filter) → `ApiKeyAuthenticationMiddleware`
  (SHA-256 + `CryptographicOperations.FixedTimeEquals`, fail-closed) → policies
  (`AdminOnly`, `TenantAccess`, `CanManageUsers/Documents/Workflows`) → MFA (TOTP +
  `MfaSetupScopeMiddleware`) → rate limiting (`RateLimitingMiddleware`, Redis-backed) →
  audit via Serilog with `Logging/SensitiveDataMasking.cs`.
- Secrets: `docker/.env` gitignored (verified via `git ls-files`); production validation at
  `Program.cs:536-593`.
- CORS fails closed when `CORS:AllowedOrigins` unset outside dev (`Program.cs:197-207`).
- **Deviation:** `tests/e2e/*.mjs` hardcode credentials
  (`admin@r2wai.io / R2wai_Admin!2026`, `browser-full-cycle-test.mjs:4-5`) and a stale port
  (3001). These scripts are unregistered (not referenced by any package.json script or CI job).
- CI security gates fail only on **Critical** CVEs (`ci.yml:165-170`, `:212-226`); High is
  report-only.

### Deployment

- `docker/`: `Dockerfile.api`, `Dockerfile.client`, `docker-compose.yml` (r2wai-studio nginx:8080,
  r2wai-api 5000→8080, redis:7-alpine, `pgvector/pgvector:pg16`), `.production.yml`,
  `.monitoring.yml`, `nginx/studio.conf`, `monitoring/prometheus.yml`, `backup.sh`, `restore.sh`,
  `.env` (gitignored) + `.env.example` + `.env.production.example`.
- `k8s/`: 8 kustomized manifests, namespace `r2wai` (namespace, configmap, redis, api-deployment,
  studio-deployment, ingress, networkpolicy).
- `.github/workflows/ci.yml` (5 jobs: backend, frontend, e2e, security, docker) and `cd.yml`
  (GHCR push → `kubectl apply -k` with rollout-status + automatic `rollout undo` + health smoke test).
- Observability: OpenTelemetry → OTLP/console (`Program.cs:273-315`), Prometheus exporter,
  Serilog console+file, 4 health checks (Database, Redis, AiProvider, Memory).

### Tests

- Backend: xUnit + Moq + `Microsoft.AspNetCore.Mvc.Testing` + EF InMemory +
  `Testcontainers.PostgreSql`. 160 test files, ~855 `[Fact]` + 83 `[Theory]` ≈ 938 methods.
  `IntegrationTestBase.cs` uses `WebApplicationFactory<Program>` on EF InMemory; 11 integration
  files use a real Postgres container and skip when Docker is absent.
- Frontend unit: 19 Vitest files (~110 cases), logic-only — **no component render tests**.
- E2E: 28 Playwright specs (45 tests) incl. `cross-tenant-isolation.spec.ts`, `security.spec.ts`.
- Widget: **zero tests**.
- No API contract/OpenAPI snapshot tests; no performance/load tests; no coverage threshold.
- Commands: `dotnet test R2WAI.slnx -c Release`; `npm run lint|test|build` in `src/R2WAI.Client`;
  `npm run build` in `src/R2WAI.Widget`; `npx playwright test` against the compose stack.

## Evidence

- Structure/counts: `R2WAI.slnx`, `Directory.Build.props`, `src/**`, `tests/**`
- Backend patterns: `src\R2WAI.Api\Program.cs`, `src\R2WAI.Api\Controllers\DepartmentsController.cs:9-63`,
  `src\R2WAI.Api\Middleware\ExceptionHandlingMiddleware.cs:49-180`
- Deviations: `src\R2WAI.Api\Controllers\AuthController.cs:21`,
  `src\R2WAI.Api\Middleware\ApiKeyAuthenticationMiddleware.cs:57`,
  `tests\browser-full-cycle-test.mjs:4-5`
- CI: `.github/workflows/ci.yml`, `.github/workflows/cd.yml`
- Tests: `tests\*.Tests\*.csproj`, `src\R2WAI.Client\vitest.config.ts`,
  `src\R2WAI.Client\playwright.config.ts`

## Conclusion

The repository is a mature, largely standards-conformant Clean Architecture codebase with strong
security middleware, real test coverage on the backend, and complete container/k8s/CI delivery.
Open deviations are tracked in `AI/reports/DOC-CODE-COMPARISON.md`.
