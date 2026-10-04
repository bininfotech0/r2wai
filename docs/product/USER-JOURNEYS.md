# User Journeys

> Status of the "Connect → Create Agent → Configure → Test → Publish → Monitor" journey against
> what's actually implemented, verified this session. See `docs/product/NAVIGATION-AND-UX.md` for
> the onboarding-journey target and `docs/audit/FEATURE-MATRIX.md` for the technical detail behind
> each status below.

| Step | Can a user do this today without Department/Application? | Evidence |
|---|---|---|
| Create an organization/admin account | Yes — first-run bootstrap via `Bootstrap__AdminEmail`/`Bootstrap__AdminPassword`, not an open registration endpoint | `Program.cs`, P0-6/P0-7 closure |
| Connect an API | Yes — `IntegrationsController`, manual or OpenAPI-imported, Application-independent | verified this session |
| Connect an MCP server | **No — not built.** | `docs/architecture/INTEGRATIONS.md` |
| Create an agent | Yes — `AssistantDefinition`, nullable `ApplicationId` | verified this session |
| Assign knowledge/tools/model | Yes | `AssistantsController`, `KnowledgeBasesController` |
| Test in a playground | Yes — real execution pipeline (`/playground` route) | not re-verified in depth this session |
| Publish as an embeddable widget | Yes, with real origin/status gating | `ChatbotsController` |
| Publish as a standalone API | **Yes, closed 2026-09-30.** `PublishedAssistantsController` — API-key-scoped (`assistant:{id}`) or Admin/SystemAdmin JWT, tool-enabled chat, pinned to the published `AssistantVersion` snapshot. | Phase 1, `docs/audit/FEATURE-MATRIX.md` |
| Get a version-pinned, rollback-safe publish | **Yes for the new standalone API path** — `ChatWithPublishedAssistantCommand` resolves every config field (system prompt, model, KB, tools, behavior settings) from the immutable snapshot, verified by editing the live assistant after publish and confirming the old values still serve. **Still live-config-bound for the chatbot widget path** — deliberately not retrofitted this phase, since `Chatbot.Chat`/`StreamChat` never actually read `AssistantDefinition` at all (confirmed by reading the code — they already run off the Chatbot's own independently-edited fields), so "pin the chatbot to a version" turned out to require a real, separate design decision about whether a linked assistant should start driving chatbot behavior at all, not a mechanical fix. | Phase 1 |
| Monitor executions/failures/approvals | Yes — Activity nav (Executions/Confirmations/Monitor) uses real persisted data | not re-verified in depth this session |
| Build a multi-step automation | Yes, via Elsa-backed workflows — but demoted to an admin-only "Legacy" nav section pending the ledger migration | `roleNav.ts` |
