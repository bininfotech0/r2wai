# Repository Inventory

> **Source:** ported from `docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md` §3–4 (2026-09-20 snapshot).
> **Status as of 2026-09-30:** the structural counts below (files, projects, migrations) are a
> point-in-time snapshot and will drift as work continues — re-run the underlying commands
> (`git ls-files`, `find src -name '*.cs' | wc -l`, etc.) before trusting exact numbers for a new
> decision. The architectural findings (deviations, technology baseline) remain accurate; nothing
> in this file has since changed.

## Snapshot (2026-09-20)

| Item | Value |
|---|---|
| Tracked files | 771 `.cs`, 116 `.tsx`, 106 `.ts`, 7 `.mjs`, 13 yaml/yml, 17 md |
| Size | ~33.6k lines C# src (excl. migrations), ~10.3k C# tests, ~22k TS/TSX client, ~1.7k e2e, 435 widget |
| Projects | Domain, Application, Infrastructure, Api + Client + Widget + 4 test projects |
| Entities / DbSets / enums | 50 entity files / 49 `DbSet` / 30 enums |
| Migrations | 44 (`20260616…InitialCreate` → `20260829…AddToolDefinitionLastTestStatus`) |
| Controllers / hubs / middleware | 25 / 3 (`/hubs/chat`, `/hubs/status`, `/hubs/notification`) / 8 |
| Hosted services | 5: Escalation, WorkflowSchedule, WorkflowDelayResume, DataRetention, BackgroundJobProcessor |
| Tests (`[Fact]/[Theory]` declarations) | Domain 150 · Application 44 · Infrastructure 123 · Api 311 · Vitest 56 · Playwright 38 |

**Since this snapshot (through 2026-09-30):** substantial work has landed on top of this baseline —
tenant isolation flipped fail-open→fail-closed with an extensive regression sweep; the
member/wallet/points feature (§44/§48 below) fully removed; several EF migrations added
(`RemoveWorkflowSchedule`, `DetachApprovalsFromWorkflows`, `AddAssistantVersions`,
`RemoveMemberRewards`, `AddWorkflowTemplateOverrides`, and others); backend test baseline is now
Domain 194/194, Application 110/110, Infrastructure 280/280, full API suite 481/482 (the one
failure is a known pre-existing Redis-unavailable environmental flake, not a regression). Re-run
the inventory commands for current exact counts; the *shape* (Clean Architecture, 4 backend
projects + Client + Widget) is unchanged.

## Technology baseline

| Item | Verdict | Note |
|---|---|---|
| .NET 10, Clean Architecture, MediatR 12.4.1, FluentValidation 11, EF Core 10, Npgsql | OK | |
| AutoMapper 16.1.1 | check | no `LicenseKey` configured (`Application/DependencyInjection.cs:23`) — newer AutoMapper is commercially licensed; verify before shipping |
| PostgreSQL 16 + pgvector, Redis 7 | OK | compose + k8s (Redis single replica; Postgres external by design) |
| MinIO | partial | client + `MinioStorageService` exist; no manifest deploys MinIO; default storage is local disk, which is per-pod under k8s replicas |
| Semantic Kernel 1.77 (+ `Plugins.Core 1.77.0-preview`) | OK / preview package | still the only AI runtime as of 2026-09-30 — Microsoft Agent Framework not integrated |
| Elsa 3.7.0 on PostgreSQL | OK | `Program.cs`; Elsa REST API not wired (deliberately) |
| SignalR, JWT, Microsoft.Identity.Web / Entra | OK | Entra is a custom token exchange (`EntraIdAuthService`) |
| Serilog, OpenTelemetry, Prometheus exporter | OK / beta package | `OpenTelemetry.Exporter.Prometheus.AspNetCore` is beta |
| Jaeger, Prometheus, Grafana | containers only | no dashboards/datasources/alerts provisioned |
| `Microsoft.Extensions.Http.Polly 10.0.0` | legacy | current recommended path is `Microsoft.Extensions.Http.Resilience` |
| React 19, TS 6, Vite 8, MUI 9, TanStack Query 5, RHF 7, Zod 3.25, React Router 7, @xyflow/react 12, SignalR client 10, Vitest 4, Playwright 1.62 | OK | `src/R2WAI.Client/package.json` |
| Widget: Vite + TS, no runtime deps | OK | `src/R2WAI.Widget/package.json` |
| Docker, Compose, K8s, Kustomize, Nginx | OK | k8s has no securityContext / NetworkPolicy / PDB |

## Architectural deviations (still accurate)

Modular monolith with Clean Architecture — the right shape, kept. Two real deviations from the
documented model:

- **Some controllers bypass the Application layer.** `ApprovalsController`, `ApiKeysController`,
  `AuthController`, `AdminController`, `WorkflowsController` inject `ApplicationDbContext`
  directly. MediatR behaviours (`AuthorizationBehavior`, `ValidationBehavior`) therefore protect
  only handlers routed through MediatR; the rest depend on per-action attributes.
- **Two workflow truths.** Elsa persists its own instance/definition/bookmark state; R2WAI
  persists `WorkflowInstance`/`WorkflowStepExecution`. The code treats R2WAI's rows as
  authoritative and restarts a *new* Elsa run for every continuation (`WorkflowBridge.cs`) — this
  is a deliberate, documented workaround for a real Elsa 3.7.x bookmark-resume bug, not an
  oversight (confirmed against Elsa's own persisted state).
