# R2WAI 2.0 Feature Gap Analysis

## Status

`VERIFIED 2026-10-04`

Compared the actual implementation against:
- `02-PRODUCT-SPECIFICATION.md`
- `03-PRODUCT-ROADMAP.md`

Evidence comes from direct repository inspection (see `CODEBASE-AUDIT.md`).

## Classification

```text
KEEP
IMPROVE
REFACTOR
REPLACE
REMOVE
DEFER
UNKNOWN
```

## Gap table

| Feature | Current implementation | Target | Gap | Priority | Evidence |
|---|---|---|---|---|---|
| Agents | `AgentRuntime`, `AgentFrameworkRuntime`, `AgentRuntimeSelector`, `AgentRuntimePolicyService` in `src\R2WAI.Infrastructure\AI\`; `Assistants` feature slice; `AssistantsController` | Agent runtime | KEEP — multiple runtime strategies exist; policy service present. Verify single entry point | P0 | `Infrastructure\AI\AgentRuntimeSelector.cs:17`, `Features\Assistants\` |
| Connections | `McpConnectionsController`, `Integrations` feature slice, `Infrastructure\Integrations\` | Universal connection engine | IMPROVE — no `IConnectionProvider` / `IConnectionProviderRegistry` abstractions named in `09-API-INTEGRATION-STANDARDS.md:100-112` | P0 | `Controllers\McpConnectionsController.cs`, grep for `IConnectionProvider` = 0 hits |
| OpenAPI discovery | `OpenApiImportService` (HTTP fetch + parse), Swashbuckle schema in `Program.cs:396-404` | API Autopilot | IMPROVE — import exists; no published OpenAPI artifact for contract testing, Swagger is Dev/Staging only | P0 | `Infrastructure\Integrations\OpenApiImportService.cs:15` |
| Parameter resolution | Validation via FluentValidation + `ValidationBehavior`; dynamic tools in `Infrastructure\AI\DynamicTools\` | Schema-driven resolution | IMPROVE — `TBD / VERIFY` whether the 8-step priority order in `09-API-INTEGRATION-STANDARDS.md:49-60` is implemented as a discrete resolver | P0 | `Application\Common\Behaviors\ValidationBehavior.cs`, `AI\DynamicTools\DynamicToolExecutor.cs:26` |
| Knowledge | `KnowledgeBases` feature slice, `Documents` slice, `Infrastructure\VectorStore` (pgvector), `AI\Prompts` | Agentic RAG | KEEP — ingestion + retrieval present. `TBD / VERIFY` citation/groundedness eval coverage | P0 | `Features\KnowledgeBases\`, `Infrastructure\VectorStore\` |
| Agent Autopilot | No matches for `Autopilot` in `src/` | Natural-language generation | UNKNOWN / DEFER — feature not found in source | P0 | grep `Autopilot` = 0 hits in `src/` |
| Playground | `src\R2WAI.Client\src\features\playground\` + `Chat`/`Runs` slices, `RunsController`, `ChatController`, SSE streaming in `ChatbotsController.cs:379` | Conversation + execution trace | KEEP — trace via `Runs` + SignalR `/hubs/chat`. Verify latency display per `07-FRONTEND-UI-UX-STANDARDS.md:75-82` | P0 | `Client\src\features\playground\`, `Controllers\RunsController.cs` |
| Publish | `PublishedAssistantsController`, `ChatWithPublishedAssistantCommand`, `ChatbotsController` public-info/chat/webhook anonymous endpoints, Widget package | Widget + API | KEEP — publish + embed path exists. Widget has zero tests | P0 | `Controllers\PublishedAssistantsController.cs:25`, `src\R2WAI.Widget\` |
| Governance | `Governance` feature slice, `ApprovalsController`, `ApiKeysController`, policies in `Program.cs:146-166`, RBAC (3 roles), `AI\Policies\` (11) | RBAC + policy + approval + audit | KEEP — all four pillars present. `TBD / VERIFY` dedicated audit record store | P0 | `Controllers\ApprovalsController.cs`, `Infrastructure\AI\Policies\` |
| Automations | Elsa 3.7 workflows (`Program.cs:65-103`), `Workflows` feature slice, `WorkflowsController`, 10 node providers, frontend `features\automations\builder\` | Simple automation engine | KEEP — engine is Elsa-based and beyond "simple". Verify `Automation` terminology maps to `Workflows` | P1 | `Api\Workflows\NodeProviders\`, `Client\src\features\automations\` |

## Rules

Do not mark a feature Complete without source/test evidence.
