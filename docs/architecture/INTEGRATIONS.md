# Integrations / Connections

## Current state

`IntegrationsController`/`ToolDefinition` (`api/v1/integrations`) is the real, already
Application-independent Connections implementation — not gated by Department or organizational
Application at all. Supports:

- Manual creation (`CreateIntegrationCommand`), a curated static catalog (`GET .../catalog`, 9
  well-known connectors, "Connect" pre-fills the same real create form — never a fake OAuth
  success), and OpenAPI import via a proven **analyze-then-commit** two-step flow
  (`AnalyzeOpenApiSpec`/`CommitOpenApiImport`): discovered operations are previewed, never
  auto-committed as callable tools.
- SSRF protection on every outbound path via the shared `EgressGuard` (`Test` button, real
  AI-invoked dispatch via `DynamicToolExecutor`, and the OpenAPI spec-fetch itself).
- Credential encryption (`IntegrationCredentialCodec`, AES-256-GCM, redacted on read).
- A separate, narrower "Connected Systems" concept (`ConnectedApplication`/`ApplicationApi`, nav
  path `/workspaces`) for registering an *application's own* API surface/versions — this one does
  require a `Department`, but it's optional and not the primary integration path; don't conflate
  the two when reading the codebase.

## MCP (closed 2026-09-30, Phase 3)

`ToolType.Mcp` is a governed connector type, mirroring the OpenAPI import flow above rather than
inventing a separate shape:

- `McpServerConnection` (`api/v1/mcp-connections`) — tenant-owned server allowlist: name, endpoint
  URL, an optional single auth-header name/value pair, credential encrypted at rest
  (`IEncryptionService`, same AES-256-GCM as everywhere else credentials are stored). CRUD +
  `toggle` + `test` (a real `ListTools` call, records `LastTestStatus`/`LastTestedAt`) all live on
  `McpConnectionsController`.
- `McpClientAdapter` wraps the verified `ModelContextProtocol.Core` v2.2.0 client
  (`McpClient.CreateAsync(HttpClientTransport, ...)`, `ListToolsAsync()`, `CallToolAsync(...)` →
  `CallToolResult.Content.OfType<TextContentBlock>()`). Every call — discovery, test, and real
  dispatch — goes through `EgressGuard.IsAllowedUrl` first, same SSRF boundary as the OpenAPI
  import path and `DynamicToolExecutor`.
- **Discover → commit**, deliberately stricter than OpenAPI's commit: `GET
  .../mcp-connections/{id}/discover` lists the server's advertised tools (nothing persisted);
  `POST .../commit` creates one `ToolDefinition` per selected tool (`ToolType.Mcp`,
  `McpServerConnectionId` + `McpToolName` set via `ToolDefinition.LinkMcpServer`) and immediately
  **deactivates** it. An MCP server's tool list is server-controlled and can change at any time, so
  nothing becomes agent-callable until an admin explicitly activates it via the existing
  `IntegrationsController` toggle (which already governs every `ToolType`, `Mcp` included) —
  OpenAPI-imported tools land active by contrast; this is an intentional, stricter default for MCP,
  not an inconsistency.
- Execution flows through Phase 2's `IToolGateway` exactly like HTTP tools — see
  `docs/architecture/TOOL-GATEWAY.md`'s "MCP adapter" section for how
  `DynamicToolFunctionFactory`/`ToolGateway`/`DeferredToolCallExecutor` were each widened to
  recognize `McpDynamicToolExecutor` alongside `DynamicToolExecutor`.

**Descope, unchanged from the plan:** HTTP transport only — no stdio/local-process MCP servers,
matching what was actually verified against the real package.
