# ADR-0001 — Application-Centric Architecture Freeze

- **Status:** Accepted, partially superseded 2026-09-29 by the R2WAI 2.0 direction in
  [ROADMAP.md](../../ROADMAP.md) — see the amendment at the bottom. The application-centric
  premise (items 1, 2, 6, 7) still holds; item 9's studio list does not.
- **Date:** 2026-08-10
- **Related:** [PLATFORM-IMPLEMENTATION-PLAN.md](../implementation/PLATFORM-IMPLEMENTATION-PLAN.md),
  [ROADMAP.md](../../ROADMAP.md) §3 (target architecture),
  [../architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) (recreated 2026-09-30, see
  the note below — not the original root-level file).

## Context

R2WAI was built as a collection of AI features (assistant, chatbot, knowledge base, workflow, model) that were **not centered on a real product scenario**. The Government AI direction requires the platform to connect existing government applications and layer AI/automation on top of them — without rebuilding the applications.

The existing domain has no `Application` or `Department` entity, and several overlapping concepts (`Chatbot` vs `AssistantDefinition` vs `Agent`), plus standalone studios that duplicate configuration surfaces.

## Decision

Freeze the following for the re-platforming effort. **No feature development proceeds until this is signed off.**

1. **`Application` is the central domain entity.** Everything (APIs, assistant, knowledge, navigation, tools, workflows, policies, security, monitoring) is configured per application. Ownership: `Tenant → Department → Application`.
2. **`Department` is a first-class entity** between `Tenant` and every application-scoped entity.
3. **The entity class is named `ConnectedApplication`** (`R2WAI.Domain.Entities.ConnectedApplication`), conceptually the "Application". The simple class name `Application` is unusable here: the `R2WAI.Application` namespace shadows it (CS0118) everywhere in the `R2WAI.*` namespace tree, because global usings are resolved *after* enclosing-namespace member lookup. A per-file `using` alias would work but is a footgun for the platform's most-used entity. User-facing concepts, table, DbSet, controller and folders keep the name `Applications`.
   > **Note (2026-09-29):** R2WAI 2.0's primary nav adds a *separate* "Connections" area
   > (governs API/MCP/model/identity connections — ROADMAP.md §2/§3). Don't conflate that with
   > this entity: `ConnectedApplication`/`Applications` is still "the business application R2WAI
   > was connected to"; "Connections" is the governed integration layer *underneath* it.
4. **`Chatbot` is merged into `Assistant → Channels`** (Phase 2 migration, not a new concept).
5. **The LLM never calls application APIs directly.** A server-side Tool/API Gateway enforces authorization (RBAC + ABAC), risk policy, confirmation, approval, and audit.
   > **Amended (2026-09-29):** R2WAI 2.0 extends this gateway to also govern MCP tool calls, not
   > just API tools — "one governed integration path" (ROADMAP.md §3). Same boundary, wider scope.
6. **RAG and live API data are separate sources** combined by the assistant, never conflated.
7. **Public is an access context, not a role** in the RBAC hierarchy.
8. **Model management is Administration → Model Governance** (super admin approves; department admin selects from approved models). The approval hierarchy is unchanged in R2WAI 2.0; the nav path has since moved to Settings → AI Models (`roleNav.ts`) and 2.0 adds configuration inheritance across global/tenant/department/application/agent scopes (ROADMAP.md §4/Phase 6), a strict widening, not a reversal.
9. ~~**Studios collapse to four areas:** Application Studio, Assistant Studio, Workflow Studio, Operations Center. Administration is a platform section.~~
   > **Superseded (2026-09-29).** R2WAI 2.0's primary nav is Home / Applications / Agents /
   > Connections / Deployments / Activity / Settings (ROADMAP.md §2). Two real changes, not
   > just relabeling:
   > - **No Workflow Studio.** Elsa and any user-facing workflow designer are being retired in
   >   favor of an R2WAI-owned durable execution ledger with no visual designer — "do not begin
   >   by recreating a generic visual workflow designer" (ROADMAP.md §3). Elsa stays wired
   >   until that ledger is built and proven (see this project's own audit memory on
   >   resequencing that cut), but it is not the long-term architecture this ADR should keep
   >   implying.
   >   - Elsa/Automations is quietly still one click away today — a `Legacy` nav section that
   >     only Admin/SystemAdmin see (`roleNav.ts`'s `LEGACY_ADMIN` section) — not deleted, just
   >     not one of the primary areas anymore. That is the intermediate state, not the target.
   >   - "Workflow Studio" as a *concept* also survives in a different shape: 2.0's durable
   >     execution ledger (Phase 3) plus the Activity area's execution timelines are its
   >     replacement, not a straight rename.
   >   - Operations Center → Activity: same job (execution timelines, approvals, failures,
   >     policy decisions), renamed and widened to include the full execution ledger's audit
   >     trail, not a v1-for-v1 swap.
   >   - Assistant Studio → Agents, Application Studio → Applications: naming-only, same scope.
   >   - **Connections and Deployments are new**, not present in this ADR's four-area list:
   >     Connections governs API/MCP/model/identity connections (item 3's note above);
   >     Deployments publishes to approved channels bound to immutable versions.
10. **No production configuration is hard-deleted.** Lifecycle: `Draft → Discovering → Configuring → Testing → Published → Disabled → Archived` with Disable/Archive/Soft Delete/Rollback. Unchanged in 2.0 — "immutable, versioned configuration", "no destructive rewrite" (ROADMAP.md §1/§6) reinforce this exact principle; the onboarding-journey verbs (`Connect → Discover → Configure → Review → Test → Publish → Monitor`) describe the *flow*, not a different lifecycle state machine.

## Consequences

- New entities (`Department`, `Application`) carry `TenantId` and inherit `BaseEntity<Guid>`, so tenant isolation and soft-delete query filters apply automatically.
- New CQRS feature folders are grouped under `Features/Applications`, `Features/Departments`, etc.
- Legacy chat-first concepts remain in the codebase until migration but are not extended.
- The `R2WAI.Application` project namespace keeps its name (the entity type name is unambiguous in practice).

## Amendment — 2026-09-29 (R2WAI 2.0)

The application-centric premise this ADR froze (items 1, 2, 6, 7, and 5/8 in spirit) is
**reaffirmed**, not reversed, by the R2WAI 2.0 direction now recorded in
[ROADMAP.md](../../ROADMAP.md) — "application first" survives as R2WAI 2.0's own stated product
principle. What actually changes:

- **Item 9's four-studio nav is superseded** by a seven-area nav (Home / Applications / Agents /
  Connections / Deployments / Activity / Settings) — see the inline note on item 9 above for what's
  a rename versus a real architectural change (no more user-facing Workflow Studio; a durable
  execution ledger replaces it; Elsa is retired to a Legacy-only nav section, not yet deleted).
- **MCP joins the governed tool boundary** item 5 already established (note on item 5).
- **Microsoft Agent Framework becomes the target agent runtime**, with Semantic Kernel kept as
  the transition adapter — this is a runtime decision this ADR never covered (it only froze the
  *entity* architecture), so it doesn't contradict anything here; ROADMAP.md §3 is the current
  record of that decision until it earns its own ADR.
- **Update (2026-09-30):** a dedicated architecture doc has returned, at a new path —
  [../architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) (plus `AI-RUNTIME.md`,
  `TOOL-GATEWAY.md`, `DATA-MODEL.md`, `INTEGRATIONS.md`, `EXECUTION-AND-WORKFLOWS.md` alongside
  it). It is not a restoration of the original root-level `ARCHITECTURE.md`; ROADMAP.md remains
  the roadmap/target-architecture reference, the new doc set covers current-vs-target state per
  subsystem.

This ADR is not superseded wholesale — the entity model (`Tenant → Department → Application`,
`ConnectedApplication`, the RAG/live-data split, public-as-context) is still the frozen decision
in force. Only item 9 is genuinely dead.
