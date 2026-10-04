# AI Runtime

## Current state (closed 2026-09-30, Phase 4)

Semantic Kernel 1.77 and Microsoft Agent Framework (`Microsoft.Agents.AI`/`Microsoft.Agents.AI.OpenAI`
v1.23.0, real official packages, verified via reflection against the restored NuGet package before
writing any adapter code — same discipline used for MCP) are both real runtimes now, selected per
tenant behind one seam.

**A real, load-bearing finding from this phase:** the plan assumed `IAgentRuntime` was already
consumed by `WorkflowsController`'s NL automation generator. Reading the actual code showed the
opposite — that controller deliberately avoids it (its own comment: `FunctionChoiceBehavior.Auto()`
would let the model call `start_workflow` instead of returning a draft, which isn't a risk worth
taking for a suggestion the admin still has to approve). `IAgentRuntime` had **zero real callers**
anywhere in the codebase before this phase. That changed the actual risk shape for the better —
widening the interface's contract couldn't break any existing caller — but it also means "prove
parity on one read-only flow" happens via dedicated tests, not a live endpoint migration, since
there was no live endpoint to migrate.

`IAgentRuntime` (`src/R2WAI.Application/Common/Interfaces/IAgentRuntime.cs`) is now request/result-
typed (`AgentRuntimeRequest`/`AgentRuntimeResult`): a correlation id always comes back (caller-
supplied or generated), and `ToolCallsMade` reflects the shared `IChatTraceCollector`'s trace for
that call — populated for free by whichever runtime ran, since both route every tool call through
`IToolGateway` (see `TOOL-GATEWAY.md`). `AgentRuntimeSelector` is what every caller actually
resolves; it picks between the two concrete runtimes per tenant via `IAgentRuntimePolicyService`
(backed by the same `GlobalPolicy` table `ToolExecutionPolicyService`/`ApprovalPolicyService`
already use — no new entity), defaulting to `AgentRuntime` (Semantic Kernel) when no policy row
exists or its content doesn't parse — fail closed to the proven runtime, never to the newer one.

**`AgentFrameworkRuntime`'s scope is deliberately narrow, not a full second runtime:**
- **OpenAI provider only.** It reads the exact same config keys `OpenAiModelProvider` does
  (`AI:OpenAI:ApiKey`/`ModelId`/`Endpoint`) and throws a clear `InvalidOperationException` if no
  key is configured — Ollama/Zai have no Agent-Framework path yet.
- **Read-only tools only** (`MafToolFunctionFactory`): HTTP tools whose method is GET/HEAD/OPTIONS
  (or unset). MCP tools are excluded outright — MCP has no HTTP-verb concept to classify as
  read-only against, so exclude rather than guess. This is plan Phase 4's explicit descope ("no
  write-capable tools on MAF yet"), enforced in code, not just documented.
- **No mid-conversation-history truncation**, unlike `SemanticKernelService.ChatAsync`'s
  `TruncateInputText` — a real, known gap for a long history, not exercised by a short prompt.
- The real bridge is `OpenAI.Chat.ChatClient.AsAIAgent(...)` (from `Microsoft.Agents.AI.OpenAI`) —
  wraps the exact same `OpenAIClient` construction `OpenAiModelProvider` already uses, straight
  into a `Microsoft.Agents.AI.ChatClientAgent : AIAgent`.

**Verification status — honest about what could and couldn't be checked:**
- Unit-tested without a real API call: `AgentRuntimePolicyService`'s fail-closed default,
  `AgentRuntimeSelector`'s per-tenant routing (SK branch runs for real over a fake `IAIService`;
  the MAF branch is proven reached by its own deterministic config-guard exception, not by a real
  model response), `MafToolFunctionFactory`'s read-only filtering (write methods and MCP tools
  excluded, name-collision handling, tenant scoping), and `AgentFrameworkRuntime`'s config guard
  itself.
- **Not verified: an actual live SK-vs-MAF response comparison.** This dev environment's
  `AI__OpenAI__ApiKey` is configured but empty (confirmed via the running `r2wai-api` container) —
  there is no real OpenAI credential available here to exercise `AgentFrameworkRuntime`'s chat/
  tool-calling path end to end. Plan Phase 4's own verification bar ("live-verify with the flag
  flipped in the real Docker stack") needs a real key to execute — flagged as open, not silently
  skipped or claimed done.

Every SK tool call passes through `AiFunctionAuditFilter`
(`src/R2WAI.Infrastructure/AI/AiFunctionAuditFilter.cs`); every MAF tool call built by
`MafToolFunctionFactory` calls `IToolGateway.InvokeAsync` directly from its own function delegate
(no MAF-side equivalent of `AiFunctionAuditFilter` — a global interceptor wasn't needed since this
runtime's tool set is built fresh, per call, already governance-filtered) — see `TOOL-GATEWAY.md`.

## Target — beyond this phase

Semantic Kernel is retained, not replaced — no package removal until a full dependency inventory
and regression pass. Widening `AgentFrameworkRuntime` to other providers (Ollama/Zai) and to
write-capable tools are real, separate follow-ons with no current trigger, not attempted here.
Durable, multi-step MAF orchestration needs the execution ledger (Phase 5) and is out of scope for
this phase regardless.
