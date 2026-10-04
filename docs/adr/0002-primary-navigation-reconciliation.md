# ADR-0002 — Primary Navigation Reconciliation

- **Status:** Accepted.
- **Date:** 2026-09-30.
- **Related:** [ADR-0001](0001-application-centric-architecture-freeze.md),
  [ROADMAP.md](../../ROADMAP.md) §2, [../product/NAVIGATION-AND-UX.md](../product/NAVIGATION-AND-UX.md),
  `src/R2WAI.Client/src/lib/nav/roleNav.ts`.

## Context

A separately-authored "Master Prompt R2WAI 2.0" brief specifies a flat 8-item primary navigation:
Home / Agents / Connections / Knowledge / Automations / Publish / Activity / Settings, and asks
that Department/organizational Application not appear as a mandatory top-level navigation module.

The codebase already has a **different**, more recently dated (2026-09-29) navigation decision in
`ROADMAP.md` §2 and `ADR-0001`'s amendment: Home / Applications / Agents / Connections /
Deployments / Activity / Settings — implemented in `roleNav.ts` as `Applications(Connected
Systems, Departments) / Agents(AI Assistants, Knowledge, Playground) / Connections(Integrations) /
Deployments(Chatbots) / Activity(Executions, Confirmations, Monitor) / Settings /
Legacy(Automations, admin-only)`.

These two targets disagree on three points:

1. Whether `Applications` (`ConnectedApplication`) appears as its own top-level nav group at all.
2. Whether `Knowledge` is a top-level item or nested under `Agents`.
3. Whether there is a `Publish` module, versus `Deployments` (narrower — chatbots only today) and
   `Automations` sitting in a trailing admin-only `Legacy` section.

A scope-check against the repository (2026-09-30) confirmed the ROADMAP/`roleNav.ts` structure is
a **deliberate, reasoned decision already recorded in code comments and ADR-0001's own
amendment** — not an oversight the master brief is correcting. Silently re-flattening navigation
to match the brief's literal 8 labels would overturn that recorded decision without addressing why
it was made.

## Decision

Keep the ROADMAP-recorded 7-area navigation as canonical. Specifically:

1. **`Applications` stays a top-level nav group.** `ConnectedApplication` is a frozen, load-bearing
   entity per ADR-0001 item 3 — this is a real business-application registration concept distinct
   from the master brief's narrower "organizational Application" (a mandatory internal grouping
   for agents). R2WAI 2.0 already satisfies the brief's actual requirement — no agent, knowledge
   base, workflow, or tool requires an `ApplicationId` — without needing to hide the entity's own
   management screen.
2. **`Knowledge` stays nested under `Agents`.** No defect or user complaint motivates promoting it;
   not changed as part of this reconciliation.
3. **`Deployments` is renamed to `Publish`, and gains a second item, once the standalone
   assistant-as-API path exists** (implementation plan Phase 1) — until then, renaming it would
   describe a capability that doesn't exist yet, violating the "no navigation item implying
   unbuilt functionality" principle both documents agree on.
4. **`Automations` is promoted out of the trailing `Legacy` section only when the durable execution
   ledger meets the exit criterion `roleNav.ts`'s own comment already names** — the ledger must
   exist and be proven before end users see Automations as a first-class area, not before.

## Consequences

- This ADR does not, by itself, change any code. Items 3 and 4 are executed by the implementation
  plan's Phase 6, gated on Phases 1 and 5 respectively landing first.
- Future references to "the 8-item navigation" from the master brief should be read through this
  reconciliation, not implemented literally.

## Update — 2026-09-30 (Phase 6 executed)

- **Item 3, done, with a correction along the way.** Phase 1 shipped `PublishedAssistantsController`
  backend-only, no client UI — so at the point Phase 6 started, "gains a second item... once the
  standalone assistant-as-API path exists" still didn't literally hold: the path existed server-side
  but nothing reachable existed for a person to use. Rather than add a nav item pointing at a
  capability an admin still couldn't actually use, Phase 6 first built the missing client piece
  (`AssistantApiAccessSection`, inside `AssistantStudioPage`'s Channels tab — create/list/toggle a
  scoped API key, a copy-pasteable curl example against `/published-assistants/{id}/chat`), reusing
  the existing generic API-key infrastructure rather than a parallel one. `Deployments` is renamed
  to `Publish` in `roleNav.ts`. It does **not** get a second top-level nav item: by the identical
  "single door, not a peer destination" reasoning `roleNav.ts` already applies to the chatbot widget
  (an assistant's own detail page, not a second sidebar entry) — consistent with this ADR's own
  "no item implying unbuilt functionality" principle now cutting the other way, toward not adding a
  redundant destination for a capability that already has a real, reachable home.
- **Item 4, not done — re-confirmed still not met, not just skipped.** The exit criterion is
  ROADMAP.md §3's full durable-execution-ledger replacement of Elsa. The implementation plan's
  Phase 5 (same 2026-09-30 pass) shipped real, narrower fixes — lease-expiry reclaim for two
  sweepers, one idempotency fix for approval notifications — but explicitly did not build the
  ledger or replace Elsa; a repository research pass during that phase confirmed workflow API-call/
  email steps still have no real crash-duplication protection (`docs/architecture/
  EXECUTION-AND-WORKFLOWS.md`). `Automations` stays in the trailing `Legacy` section. `roleNav.ts`'s
  own comment above `LEGACY_ADMIN` was updated to record this re-check rather than leave a reader to
  wonder whether it was simply forgotten.
