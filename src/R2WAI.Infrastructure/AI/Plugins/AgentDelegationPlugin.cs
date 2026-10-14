using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Plugins;

/// <summary>
/// Real multi-agent delegation, deliberately built as "another governed tool call" rather than a
/// parallel orchestration engine: both functions below run through the same Semantic Kernel
/// function-invocation filter (AiFunctionAuditFilter -> ToolGateway) as every other built-in plugin,
/// so a delegation inherits audit/risk/approval/ledger for free and shows up in the existing
/// Activity/Runs UI with no new surface. See BuiltInToolGovernance for its default risk level.
///
/// Scoped narrowly: a delegated sub-agent runs with the target assistant's own SystemPrompt and
/// enabled tools, through the SAME IAgentRuntime (and therefore the same model/runtime) the caller
/// is already running under — NOT a different model per sub-agent. Routing a sub-agent to a
/// genuinely different model would need IAgentRuntime/IAIService.ChatAsync to accept a per-call
/// ResolvedModelConfig override, which today is only resolved once per kernel build; a real,
/// separate change, not attempted here.
/// </summary>
public class AgentDelegationPlugin
{
    private readonly ICurrentUserService _currentUser;
    private readonly IEnabledToolScope _enabledToolScope;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentDelegationPlugin> _logger;

    public AgentDelegationPlugin(
        ICurrentUserService currentUser,
        IEnabledToolScope enabledToolScope,
        IServiceScopeFactory scopeFactory,
        ILogger<AgentDelegationPlugin> logger)
    {
        _currentUser = currentUser;
        _enabledToolScope = enabledToolScope;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [KernelFunction("delegate_to_agent")]
    [Description("Hand a task off to another specialist agent this assistant is configured to work with. Use this when part of the user's request is better handled by a different agent's own expertise, tools, or knowledge than by answering it directly.")]
    [return: Description("The delegated agent's response to the task")]
    public async Task<string> DelegateToAgentAsync(
        [Description("The id of the agent to delegate to — must be one of this assistant's allowed delegates")] Guid assistantId,
        [Description("The specific task or question to hand to that agent")] string task,
        CancellationToken ct = default)
    {
        var result = await DelegateOneAsync(assistantId, task, ct);
        return result.Text;
    }

    [KernelFunction("delegate_to_agents")]
    [Description("Fan a complex request out to several specialist agents at once instead of one at a time, then see all their responses together. Use this when parts of the request are independent and can be handled in parallel.")]
    [return: Description("Each agent's response, labeled by which agent handled it")]
    public async Task<string> DelegateToAgentsAsync(
        [Description("JSON array of objects, one per agent to delegate to: [{\"assistantId\": \"...\", \"task\": \"...\"}]")] string tasksJson,
        CancellationToken ct = default)
    {
        List<DelegationTaskInput>? tasks;
        try
        {
            // Case-insensitive: the model is instructed (see this function's own [Description]) to
            // produce lowercase camelCase keys, which don't match these PascalCase record properties
            // under System.Text.Json's case-sensitive default — every field would silently bind to
            // its type's default (Guid.Empty, null) instead of throwing, turning a working-looking
            // call into a confusing "not configured to delegate" denial for every target.
            tasks = JsonSerializer.Deserialize<List<DelegationTaskInput>>(
                tasksJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return "Error: tasks must be a JSON array of {assistantId, task} objects.";
        }

        if (tasks is null || tasks.Count == 0)
            return "Error: no tasks provided.";

        if (tasks.Any(t => t.AssistantId == Guid.Empty || string.IsNullOrWhiteSpace(t.Task)))
            return "Error: every task needs a non-empty assistantId and task.";

        // Task.Run, not a bare DelegateOneAsync(...) in the Select — each branch must get its own
        // independent copy of the ambient DelegationDepth so a sibling's increment (from entering
        // its own TryEnter()) can never leak into another sibling before either one's first real
        // await. Task.Run's documented ExecutionContext isolation guarantees that; a plain async
        // call does not; this was verified against the .NET ExecutionContext/AsyncLocal contract,
        // not assumed.
        var results = await Task.WhenAll(
            tasks.Select(t => Task.Run(() => DelegateOneAsync(t.AssistantId, t.Task, ct), ct)));

        var sb = new StringBuilder();
        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.AssistantName} ---");
            sb.AppendLine(result.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task<DelegationResult> DelegateOneAsync(Guid assistantId, string task, CancellationToken ct)
    {
        using var depthScope = DelegationDepth.TryEnter();
        if (depthScope is null)
        {
            _logger.LogWarning("Delegation to {AssistantId} denied: maximum delegation depth ({MaxDepth}) reached", assistantId, DelegationDepth.MaxDepth);
            return new DelegationResult("(denied)", "Delegation denied: the maximum delegation depth was reached for this request.");
        }

        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("User tenant not found");

        // Defense-in-depth, same shape as AiFunctionAuditFilter.IsEnabledForCallingAssistant: the
        // calling assistant must explicitly allow-list this target. Checked here, not just in the
        // Studio UI, so a prompt can't talk its way into delegating to an un-listed agent. Reads
        // the plugin's own ambient IEnabledToolScope — set once for this turn by whichever chat
        // entry point started it — which is safe to read concurrently from parallel branches since
        // nothing mutates it during a turn.
        if (!_enabledToolScope.DelegatableAssistantIds.Contains(assistantId))
        {
            _logger.LogWarning("Delegation to {AssistantId} denied: not in the calling assistant's delegate allow-list", assistantId);
            return new DelegationResult("(denied)", "Delegation denied: this agent is not configured to delegate to that assistant.");
        }

        // An isolated scope, not the ambient one: delegate_to_agents runs branches concurrently
        // (see its own Task.Run comment), and the ambient IRepository<AssistantDefinition>/
        // IAgentRuntime are backed by this request's single scoped DbContext, which EF Core does
        // not allow two operations on at once ("a second operation was started on this context
        // before a previous operation completed" — the same failure class ApprovalService's own
        // NotifyRequesterOfDecisionAsync comment documents hitting and fixing this exact way).
        // Applied to the single-delegation path too, defensively: nothing here depends on sharing
        // the caller's DbContext, and it costs one extra scope to not have to prove SK's own
        // auto-function-calling loop never dispatches two tool calls concurrently in one turn.
        using var scope = _scopeFactory.CreateScope();
        var assistants = scope.ServiceProvider.GetRequiredService<IRepository<AssistantDefinition>>();
        var agentRuntime = scope.ServiceProvider.GetRequiredService<IAgentRuntime>();

        var target = await assistants.FirstOrDefaultAsync(a => a.Id == assistantId && a.TenantId == tenantId, ct);
        if (target is null || !target.IsActive)
        {
            _logger.LogWarning("Delegation to {AssistantId} failed: target does not exist, is inactive, or belongs to a different tenant", assistantId);
            return new DelegationResult("(unavailable)", "Delegation failed: the target agent no longer exists or is inactive.");
        }

        // DelegatableAssistantIds is threaded through too, bounded by DelegationDepth.MaxDepth —
        // a sub-agent may itself delegate further, within the same depth cap already entered above.
        // Resolving IAgentRuntime from this same isolated scope means any further nesting (the
        // sub-agent itself calling delegate_to_agent) gets its own IEnabledToolScope instance too,
        // populated fresh from target's own fields inside GetOrCreateKernelAsync — not stale state
        // inherited from this call's ambient scope.
        var result = await agentRuntime.InvokeAsync(
            new AgentRuntimeRequest(
                Message: task,
                SystemPrompt: target.SystemPrompt,
                EnabledToolIds: target.GetEnabledToolIds(),
                DelegatableAssistantIds: target.GetDelegatableAssistantIds()),
            ct);

        return new DelegationResult(target.Name, result.Text);
    }

    private sealed record DelegationResult(string AssistantName, string Text);
    private sealed record DelegationTaskInput(Guid AssistantId, string Task);
}
