# Tool Gateway

## Current state (closed 2026-09-30, Phase 2)

`IToolGateway` (`src/R2WAI.Application/Common/Interfaces/IToolGateway.cs`) is now the real,
runtime-agnostic governance boundary, implemented by `ToolGateway`
(`src/R2WAI.Infrastructure/AI/ToolGateway.cs`): resolves the calling `ToolDefinition` (by caller-
supplied id when given, else by name, else a built-in default), evaluates governance (unknown tool
⇒ deny, fail-closed; role check; tenant policy risk ceiling; approval requirement; defense-in-depth
check against the calling assistant's enabled-tool list), and on Allow times the call, records the
result to `IChatTraceCollector` (ephemeral, per-turn) and to `AuditLog` (durable, `durationMs`
included, raw arguments deliberately excluded pending a redaction design). A denied-pending-
approval HTTP/dynamic tool call creates a real, replayable `ApprovalRequest`; built-in SK-plugin
tools still deny outright (no re-invocation path outside a live Kernel).

`AiFunctionAuditFilter` (`src/R2WAI.Infrastructure/AI/AiFunctionAuditFilter.cs`) is now a thin
Semantic Kernel adapter: extracts plugin/function/arguments/tool-definition-id from
`FunctionInvocationContext`, builds a `ToolInvocationRequest`, calls `IToolGateway.InvokeAsync`,
and translates the outcome back into `context.Result`. Its constructor shrank from 8 dependencies
to 1 (`IToolGateway`). The pure decision helpers (`EvaluateGovernance`, `IsEnabledForCallingAssistant`,
`HumanizeFunctionName`) and the `ToolDefinitionIdMetadataKey` constant deliberately **stayed** on
`AiFunctionAuditFilter` rather than moving to `ToolGateway` — `IntegrationsController`'s "Test"
button and `DynamicToolFunctionFactory`'s SK metadata stamping already reference them at that
location, and moving them would have been a second, unrelated rename bundled into this extraction.

`DynamicToolExecutor` is still the actual HTTP dispatch layer underneath, gated by the shared
`EgressGuard` SSRF check — unchanged by this extraction.

**Verified behavior-preserving, not just built:** the full `ToolGovernanceFilterTests.cs` suite
(11 tests — unregistered-tool denial, dynamic-tool-by-id governance, real deferred approval-request
creation, built-in-tool approval still denying outright, a low-risk tool actually running,
cross-tenant denial, tenant-with-no-rows built-in default, utility-plugin bypass, duration
tracking) passes unchanged against the real Kernel + real DI-resolved filter + real repos — this
is the strongest available proof the refactor changed nothing observable. Plus the full fast
suites and a 185-test Approval/Integration/Chatbot/Workflow slice, all green (the one failure was
the same pre-existing Redis-unavailable environmental flake documented elsewhere in this repo).

## MCP adapter (closed 2026-09-30, Phase 3)

MCP tools flow through the exact same `IToolGateway`/`AiFunctionAuditFilter` pipeline as HTTP
tools — no second governance implementation. `DynamicToolFunctionFactory.BuildPluginAsync` now
runs two queries (`ToolType.Http` with `ApplicationApi` included, `ToolType.Mcp` with
`McpServerConnection` included — EF's string-path `Include` only supports one navigation per
query, so this is two narrow queries rather than a repository-interface change) and builds a
Kernel function for each, dispatching to `DynamicToolExecutor` or the new `McpDynamicToolExecutor`
based on `ToolDefinition.ToolType`. The deferred-approval replay path
(`ToolGateway`'s `DenyApprovalRequired` branch → `DeferredToolCallExecutor`) was widened the same
way, so an approval-required MCP tool call gets the identical create-request-now/replay-on-approval
treatment HTTP tools already had — this closes a gap that would otherwise have silently swallowed
approved MCP calls (the HTTP executor would report "not linked to a registered API" for an MCP-
shaped row).

See `docs/architecture/INTEGRATIONS.md` for the MCP connector shape (`McpServerConnection`,
`McpClientAdapter`, `McpConnectionsController`'s discover/commit two-step) and the target/descope
notes (HTTP transport only, discovered tools land inactive).

## Target — Agent Framework adapter

A future Agent Framework adapter (Phase 4) builds the same `ToolInvocationRequest` shape from its
own call site and delegates to the identical `IToolGateway` — no schema change,
`ToolDefinition`'s existing risk/role/approval columns remain the governance record.
