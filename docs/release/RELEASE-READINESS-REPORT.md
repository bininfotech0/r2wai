# R2WAI 2.0 — Release Readiness Evidence Report

Required by `R2WAI-2.0-Complete-Development-Documentation/12-PRODUCTION-READINESS.md` §8.

This report classifies every §1–§7 checklist item as `VERIFIED` / `PARTIAL` / `UNVERIFIED` /
`MISSING` / `BLOCKED`, per doc 07 §16. **Nothing here is inferred.** `VERIFIED` means a command was
run in this environment and its output is recorded. Where no test was executed, the item is marked
`UNVERIFIED` rather than optimistically `VERIFIED` — a code path being *present* is not evidence
that it *works*.

## Environment

| Field | Value |
|---|---|
| Date | 2026-10-01 |
| Commit | `1c9634683` |
| Branch | `feature/react-studio-migration` |
| Host OS | Windows (dev workstation) |
| .NET SDK | 10.0.204 (no `global.json` — floating) |
| Node | v24.18.0 |
| Docker | 29.7.2 (Docker Desktop) |
| Database | PostgreSQL 16.15 + pgvector (`pgvector/pgvector:pg16`) |
| Test DBs | throwaway containers `r2wai-mig58-verify` / `r2wai-uniq`, since removed |

## Test totals actually executed

| Suite | Result |
|---|---|
| `R2WAI.Domain.Tests` | 200 passed, 0 failed |
| `R2WAI.Application.Tests` | 124 passed, 0 failed |
| `R2WAI.Infrastructure.Tests` | 343 passed, 0 failed |
| `R2WAI.Api.Tests` | 531 passed, 0 failed |
| **Backend total** | **1198 passed, 0 failed** |
| Tenant/security/isolation subset | 275 passed, 0 failed |
| `R2WAI.Client` vitest | 101 passed (17 files), 0 failed |
| `npm run lint` | warnings only, 0 errors; none in files touched by Phase 1 |
| `npm run build` (`tsc -b` + vite) | pass |
| `dotnet build R2WAI.slnx -c Release --no-incremental` | **0 errors, 315 warnings** — see note below |
| Playwright E2E | harness validated — `route-smoke.spec.ts` **passed** against the running stack. Full suite **not run** on the Phase 1 build |
| Widget build (`src/R2WAI.Widget`) | **pass** — `tsc --noEmit` clean, `vite build` emitted `dist/widget.js` (11.44 kB, 3 modules) |

> Counting note: the plan's earlier "927 backend tests" was a count of `[Fact]`/`[Theory]`
> *attributes*. Executed cases are **1198** (`[Theory]` expands into multiple cases).

> Warning-count correction: an earlier note in this report cited 12 warnings. That came from an
> **incremental** `Debug` build, which skipped recompilation and therefore re-emitted almost no
> warnings. A full `--no-incremental` `Release` compile emits **315**. The two warnings that appear
> in files Phase 1 touched are both pre-existing and were individually checked, not assumed:
> `ConnectedApplication.cs(33)` CS8618 on `Name`/`Code` comes from the unchanged EF parameterless
> constructor, and IDE0161 in the new migrations is the block-scoped-namespace style that **all 60**
> EF-generated migrations in the repo already use.

---

## 1. Product

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 1.1 | Agent creation does not require Department/Application | `AssistantDefinition.ApplicationId` already `Guid?`; Connections path made optional in Phase 1. 10 tests in `CreateApplicationCommandHandlerTests` + `ConnectedApplicationTests`; verified at DB layer against live PostgreSQL | **VERIFIED** | unassigned | None for creation. See §2.5 for duplicate-Code semantics |
| 1.2 | Onboarding works | No onboarding flow exercised | **UNVERIFIED** | unassigned | Unknown |
| 1.3 | Connections work | CRUD implemented; `CreateApplicationCommandHandlerTests` cover create + tenant rejection. E2E not run against the new build | **PARTIAL** | unassigned | Full UI journey unproven on new build |
| 1.4 | Knowledge works | Not exercised in this pass | **UNVERIFIED** | unassigned | Vector store has no tenant column (§3.3) |
| 1.5 | Automations work within documented scope | Not exercised. ADR-0002 records missing crash-duplication protection on API-call/email steps | **PARTIAL** | unassigned | Durability gap is documented, not fixed |
| 1.6 | Publishing works | Not exercised in this pass | **UNVERIFIED** | unassigned | Unknown |
| 1.7 | Activity is real and persisted | `AuditLog` writes skip when `TenantId` is null (`ApplicationDbContext.cs:266-267`) — reachable only pre-tenant, not audited for reachability | **PARTIAL** | unassigned | Bootstrap-only path not proven unreachable |

## 2. Architecture

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 2.1 | Clean dependency direction | Api → Infrastructure → Application → Domain compiles clean; 0 errors | **VERIFIED** | unassigned | None observed |
| 2.2 | No unintended duplicate runtime/workflow ownership | Both Semantic Kernel and Microsoft Agent Framework are live, selected per tenant (`AgentRuntimePolicyService`); SK remains default and fallback | **VERIFIED** | unassigned | Dual runtime is intentional; do not collapse |
| 2.3 | AI provider abstraction verified | Ollama/OpenAI/Zai reached through `ModelGateway`; `/health/ready` reports `ai-providers` Healthy | **VERIFIED** | unassigned | None observed |
| 2.4 | Tool gateway enforced | SK path reaches `ToolGateway`; `GovernedKernelBuilder` returns tool-less kernel on governance failure | **PARTIAL** | unassigned | **MAF runtime parity unconfirmed** — `MafToolFunctionFactory` is read-only by design; must prove no HTTP tool bypasses |
| 2.5 | Durable execution verified where required | ADR-0002 re-checked 2026-09-30: API-call/email workflow steps still lack real crash-duplication protection | **MISSING** | unassigned | Prevents Automations promotion; blocks HPA scale-out |

## 3. Security

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 3.1 | Authentication | Not exercised in this pass | **UNVERIFIED** | unassigned | PBKDF2 100k / 15-min access / 7-day refresh are code-level only |
| 3.2 | Authorization | RBAC tests within the 275-test subset pass | **PARTIAL** | unassigned | Not traced per-endpoint |
| 3.3 | Tenant isolation | **275 tests pass** filtered on `Tenant\|Security\|CrossTenant`. Phase 1 added explicit cross-tenant department rejection (`Handle_DepartmentFromAnotherTenant_ThrowsNotFound`). `cross-tenant-isolation.spec.ts` exists but was **not run** | **PARTIAL** | unassigned | **Vector store has no tenant column** — retrieval path not proven tenant-safe |
| 3.4 | Secrets | `docker/.env` present; `.gitignore` in `src/R2WAI.Client` does **not** exclude `.env` | **PARTIAL** | unassigned | Client `.env` can be committed |
| 3.5 | SSRF | Not exercised | **UNVERIFIED** | unassigned | Unknown |
| 3.6 | Prompt injection / tool abuse | Fail-closed `DenyUnknownTool` behaviour implemented; not adversarially tested | **PARTIAL** | unassigned | No injection test corpus |
| 3.7 | Audit | See 1.7 | **PARTIAL** | unassigned | Correlation-ID searchability unconfirmed |
| 3.8 | Rate limits | `RateLimitingMiddleware` registered (`Program.cs:393`); not load-tested | **PARTIAL** | unassigned | No evidence of effective limits |
| 3.9 | Critical findings resolved | No formal critical-findings assessment has been performed | **MISSING** | unassigned | **Release blocker** — see §9 |
| 3.10 | Vulnerable dependencies | `dotnet list R2WAI.slnx package --vulnerable --include-transitive` run 2026-10-01. **All four production projects report no vulnerable packages.** Two High-severity transitive advisories affect **test projects only**: `System.Security.Cryptography.Xml` 10.0.7 (GHSA-cvvh-rhrc-wg4q and four further advisories) and `SSH.NET` 2024.2.0 (GHSA-q939-rpr3-3284), both pulled in transitively by Testcontainers | **PARTIAL** | unassigned | Not in the shipped artifact; supply-chain hygiene for CI. Should still be bumped |

## 4. Database

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 4.1 | Migration tested | **Migrations 1–59 applied, reverted and round-tripped on real PostgreSQL 16.15 + pgvector.** Data preserved, all indexes and FKs intact, migration count consistent. See plan §10.4, §10.6 | **VERIFIED** | unassigned | #58 and #59 must ship in the same release |
| 4.2 | Backup tested | `docker/backup.sh` exists (1723 bytes); never executed | **MISSING** | unassigned | Unproven |
| 4.3 | Restore tested | `docker/restore.sh` exists (2128 bytes); never executed | **MISSING** | unassigned | **RPO/RTO unproven** |
| 4.4 | Indexes reviewed | `Applications` indexes reviewed and a gap found and closed (partial unique index #59). Remaining tables not reviewed | **PARTIAL** | unassigned | Other tables unreviewed |
| 4.5 | Tenant tests pass | Within the 275-test subset | **VERIFIED** | unassigned | Vector store excluded (3.3) |
| 4.6 | Legacy migration verified | 57 pre-existing migrations applied forward on a clean database without error | **VERIFIED** | unassigned | Idempotent-script generation is **broken** (see below) |

> **Known pre-existing defect found during this pass:** `dotnet ef migrations script --idempotent`
> fails at migration `20260625123421_EnrichAssistantDefinition`, whose raw SQL lacks a trailing
> semicolon, so EF's `DO $EF$` wrapper emits `…WHERE "IsActive" = true` immediately followed by
> `END IF`. Normal `database update` is unaffected. Not introduced by Phase 1; not fixed.

## 5. AI

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 5.1 | Provider connectivity | `/health/ready` reports `ai-providers` Healthy against the running stack | **VERIFIED** | unassigned | Ollama-backed; no approved-model sign-off |
| 5.2 | Capability validation | Not exercised | **UNVERIFIED** | unassigned | Unknown |
| 5.3 | Tool calling | Governed path implemented; not exercised end-to-end in this pass | **PARTIAL** | unassigned | Depends on 2.4 |
| 5.4 | Retrieval | Not exercised | **UNVERIFIED** | unassigned | Depends on 3.3 |
| 5.5 | Citations where required | Not exercised | **UNVERIFIED** | unassigned | Unknown |
| 5.6 | Limits / timeouts / cancellation | Not exercised | **UNVERIFIED** | unassigned | Unknown |
| 5.7 | No silent cloud fallback | Providers reached via `ModelGateway`; no direct SDK calls observed in the audited path | **PARTIAL** | unassigned | Static reading only; runtime egress unmonitored |

## 6. Operations

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 6.1 | Health checks | `GET /health` → 200 Healthy (includes `database`); `GET /health/ready` → 200 (includes `database`, `redis`, `ai-providers`). Containers report healthy | **VERIFIED** | unassigned | None observed |
| 6.2 | Structured logs | Serilog configured; format not asserted in a test | **PARTIAL** | unassigned | Schema stability unproven |
| 6.3 | Correlation IDs | `CorrelationIdMiddleware` present; not confirmed returned to client or searchable | **PARTIAL** | unassigned | Audit retrieval path unproven |
| 6.4 | Failure visibility | Prometheus + Grafana compose config exists; Prometheus exporter pinned to `1.18.0-beta.1` | **PARTIAL** | unassigned | **Beta dependency in a production scrape path** |
| 6.5 | Capacity monitoring | Dashboards exist in config; not verified against live traffic | **PARTIAL** | unassigned | No load data |

## 7. Deployment

| # | Requirement | Evidence | Result | Owner | Remaining risk |
|---|---|---|---|---|---|
| 7.1 | Clean install | Compose stack reached healthy state and served HTTP 200 on :8080 | **PARTIAL** | unassigned | Built from a **pre-Phase-1 image**; new code not containerised or exercised |
| 7.2 | Upgrade | Not tested across a version boundary | **MISSING** | unassigned | **#58/#59 must ship together**; preflight guard covers the split case |
| 7.3 | Rollback | Migration `Down` paths verified on a disposable database (#58 refuses loudly with NULL rows and leaves data untouched; #59 drops an index only). Container-level rollback untested | **PARTIAL** | unassigned | DB rollback proven, image rollback not |
| 7.4 | TLS | `UseHttpsRedirection` present (`Program.cs:389`); nginx terminates in front | **UNVERIFIED** | unassigned | Dev stack served plain HTTP on :8080 |
| 7.5 | CORS | Four named policies configured; not verified at runtime | **PARTIAL** | unassigned | Policy selection unproven |
| 7.6 | Secure secret injection | Compose injects `DB_PASSWORD` via env; no secret manager | **PARTIAL** | unassigned | Plain env vars |
| 7.7 | Persistent storage | Postgres and Redis volumes declared | **PARTIAL** | unassigned | Not verified across a recreate |
| 7.8 | Redis has no password | Confirmed in compose and `k8s/redis.yaml` | **MISSING** | unassigned | Network isolation is the only control |

---

## 8. Phase 1 record (Department decoupling)

Fully documented in `14-IMPLEMENTATION-PLAN.md` §10, including two defects found during
verification and fixed: a `Down` that corrupted data, and a `Code`-uniqueness gap exposed by
nullability. Summary:

| Gate | Result |
|---|---|
| Connection creatable with no Department (UI + API + DB) | **VERIFIED** |
| `/departments` still reachable | **VERIFIED** (retained; ADR-0002 forbids hiding it) |
| Migration additive, no row deleted | **VERIFIED** |
| Cross-tenant Department id rejected | **VERIFIED** |
| Duplicate `Code` for department-less system rejected | **VERIFIED** |
| Existing dept-scoped uniqueness unchanged | **VERIFIED** |

### 8.1 E2E coverage added for the department-less path

`connected-systems.spec.ts` previously seeded a Department in every test, so the headline Phase 1
capability had **no** browser-level coverage. Two specs were added:

| Spec | Purpose |
|---|---|
| `a connected system can be created with no department at all` | Confirms the field is labelled `Department (optional)`, defaults to `None`, that creating with only Name + Code succeeds, that the list renders the missing department as `—` (located by header, not a hardcoded index), then deletes via the detail page |
| `a duplicate Code for a department-less system is rejected` | Two department-less systems sharing one `Code`; the second must be rejected |

Both specs compile (`tsc` clean, `eslint` clean) and are discoverable — `playwright --list` reports
4 tests in the file. They were then run against the **current pre-Phase-1 container image** and
**both failed, for the correct reasons**:

- spec 1 failed on its first assertion — the old UI labels the field `Department`, not `Department (optional)`;
- spec 2 failed with the **Create button disabled** — the old form still requires a Department.

This is the intended red state: the specs genuinely detect the old behaviour and can only go green
once the stack is rebuilt from the Phase 1 code. They remain **UNVERIFIED against the new build**
until that rebuild happens.

One correction worth recording: the first draft of these specs tried to delete rows via
`row.getByLabel('Delete')`. `ApplicationsPage`'s column definitions contain no actions column —
unlike `DepartmentsPage`, which has one — so that cleanup would have failed. Both specs were
rewritten to delete from the workspace detail page.

## 9. Outstanding blockers before production

1. **No formal critical-findings assessment exists** (3.9). Everything above is scoped evidence, not
   a security review.
2. **Backup and restore are untested** (4.2, 4.3). RPO/RTO are unknown.
3. **Vector store has no tenant column** (3.3). A retrieval path that is not tenant-scoped is a
   data-exposure risk, not a backlog item.
4. **MAF tool-gateway parity unproven** (2.4).
5. **Durable execution missing for API-call/email steps** (2.5). Also why the API HPA is pinned to 1
   replica: four sweepers are not lease-based, so `replicas > 1` is currently unsafe.
6. **Playwright E2E not run** against the Phase 1 build. 27 specs exist; the harness is proven
   working (`route-smoke` passes), but the running stack serves a **pre-Phase-1 image**, so its
   results say nothing about these changes. Requires a container rebuild before it is meaningful.
   Separately, no spec covers the new capability — `connected-systems.spec.ts` and
   `departments.spec.ts` always seed a department, so "create a connected system with no
   department" has **no E2E coverage** and should be added when the stack is rebuilt.
7. **Beta dependency in production telemetry** (6.4): Prometheus exporter `1.18.0-beta.1`.
8. **`dotnet ef migrations script --idempotent` is broken** (§4 note) — will break any DBA tooling
   that generates idempotent scripts.

## 10. Production blockers requiring external confirmation

Per doc 12 §9, these cannot be settled from the repository and need sign-off:

- Actual workload / concurrency targets
- RPO / RTO
- Data retention policy
- Compliance requirements
- Approved models and connections
- Network egress policy
- Enterprise identity integration
- Disaster recovery plan

**Owner is unassigned for every row above.** This report records mechanical evidence only; it is not
an approval to ship.