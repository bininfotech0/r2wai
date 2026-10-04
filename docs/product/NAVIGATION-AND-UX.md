# Navigation and UX

> **Source:** target nav ported from `ROADMAP.md` §2 (2026-09-29); current-state nav verified
> directly against `src/R2WAI.Client/src/lib/nav/roleNav.ts`. See
> `docs/adr/0002-primary-navigation-reconciliation.md` for how the two are reconciled.

## Target primary navigation (ROADMAP §2)

| Navigation | Purpose |
|---|---|
| Home | Tenant/application overview, setup checklist, recent activity and operational status |
| Applications | Register and manage business applications; discover APIs and configure application-scoped AI capabilities |
| Agents | Create, configure, test, version, and publish assistants/agents |
| Connections | Govern API, MCP, model, identity, and other external-system connections |
| Deployments | Publish to approved channels such as embedded widget and application-facing interfaces |
| Activity | Execution ledger, tool calls, approvals, policy decisions, audit, and troubleshooting |
| Settings | Users/roles, tenant and department settings, security, model policy, retention, and platform configuration |

This is a **navigation consolidation**, not a backend deletion plan — existing modules should be
surfaced in the relevant context, not deleted, and existing records/domain boundaries are kept
unless a reviewed migration explicitly replaces them.

## Current navigation (verified against `roleNav.ts`, 2026-09-30)

`Applications(Connected Systems, Departments) / Agents(AI Assistants, Knowledge, Playground) /
Connections(Integrations[, Tools&APIs, AI Models for SuperAdmin]) / Publish(Chatbots) /
Activity(Executions, Confirmations, Monitor) / Settings / Legacy(Automations, admin-only)`.
`Deployments` was renamed to `Publish` by ADR-0002's Phase 6 update — this line previously still
said `Deployments`, one step behind that rename; kept in sync now.

This matches the ROADMAP target closely but not exactly: Knowledge is nested under Agents rather
than being its own top-level entry, and Automations is deliberately demoted to a trailing
"Legacy" section pending the durable-execution-ledger migration (a documented decision in
`roleNav.ts`'s own comments, not an oversight).

## Core onboarding journey (ROADMAP §2)

**Connect → Discover → Configure → Review → Test → Publish → Monitor**

- Connect an application using a supported OpenAPI/Swagger definition, MCP server, or explicitly
  supported connection type.
- Discover endpoints, schemas, authentication requirements, and candidate capabilities — treat
  discovered content as untrusted input.
- Generate a *draft* configuration: suggested tools, knowledge sources, access scopes, and
  assistant instructions. Never automatically activate discovered write-capable tools.
- Review permissions, risk classification, data handling, and approval requirements.
- Test using authorized test identities and representative cases.
- Publish an immutable, versioned configuration after authorization and policy validation.
- Monitor real executions, denials, approvals, model usage, and failures.

**Status:** the OpenAPI half of this journey is real today (`IntegrationsController`'s
analyze-then-commit flow, discovered operations never auto-activated). The MCP half doesn't exist
yet (plan Phase 3). "Publish an immutable, versioned configuration" is not yet true for assistants
served through a chatbot — they currently resolve the *live* config, not a pinned version; plan
Phase 1 closes this specifically.
