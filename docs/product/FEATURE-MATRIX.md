# Product Feature Matrix

> User-facing view of the 8 target product modules. For the underlying technical feature-by-feature
> status, see `docs/audit/FEATURE-MATRIX.md`.

| Module | Target responsibility | Status |
|---|---|---|
| Home | Real dashboard and onboarding guidance | Not audited in depth this session |
| Agents | Agent lifecycle, configuration, versions, testing | Working — create/configure/test/version/rollback all real; serve-time version pinning still missing (plan Phase 1) |
| Connections | REST, MCP, databases, and approved external integrations | REST/OpenAPI working end to end with SSRF protection and admin review before activation; MCP not built (plan Phase 3); database connections not audited |
| Knowledge | Secure document and knowledge management | Working — tenant-safe retrieval verified by tracing the real code; crawling/sync scheduling not built |
| Automations | Visual workflows, scheduling, approvals | Working underneath (Elsa-backed, real approval lifecycle) but demoted to an admin-only "Legacy" nav section pending the durable-execution-ledger migration |
| Publish | API and widget publishing, version management | Widget publishing real and origin-gated; no unified module, no standalone assistant-as-API, no enforced version pinning (plan Phase 1) |
| Activity | Execution history, audit, operational diagnostics | Present (Executions/Confirmations/Monitor); not audited in depth this session |
| Settings | Organization, identity, models, policies, security | Present; not audited in depth this session |

**Not a literal 1:1 with the current sidebar** — see `docs/product/NAVIGATION-AND-UX.md` and
`docs/adr/0002-primary-navigation-reconciliation.md` for how today's nav (`Applications` as its
own top-level group, `Knowledge` nested under `Agents`) maps onto these 8 target modules.
