using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// The <see cref="IAgentRuntime"/> every caller actually resolves — picks between the Semantic-
/// Kernel-backed <see cref="AgentRuntime"/> and the Microsoft-Agent-Framework-backed
/// <see cref="AgentFrameworkRuntime"/> per tenant (<see cref="IAgentRuntimePolicyService"/>),
/// implementation plan Phase 4. Depends on the two concrete classes rather than a second
/// <c>IAgentRuntime</c> registration each, so DI can't accidentally resolve this selector into
/// itself.
///
/// Unlike <c>IWorkflowBridge</c>'s swap (an environment-wide DI registration, decided once at
/// startup), this decision is per-tenant and can't be made at container-build time — so the
/// selector, not the DI container, is the swap point.
/// </summary>
public class AgentRuntimeSelector : IAgentRuntime
{
    private readonly AgentRuntime _semanticKernelRuntime;
    private readonly AgentFrameworkRuntime _agentFrameworkRuntime;
    private readonly IAgentRuntimePolicyService _policy;
    private readonly ICurrentUserService _currentUser;

    public AgentRuntimeSelector(
        AgentRuntime semanticKernelRuntime, AgentFrameworkRuntime agentFrameworkRuntime,
        IAgentRuntimePolicyService policy, ICurrentUserService currentUser)
    {
        _semanticKernelRuntime = semanticKernelRuntime;
        _agentFrameworkRuntime = agentFrameworkRuntime;
        _policy = policy;
        _currentUser = currentUser;
    }

    public async Task<AgentRuntimeResult> InvokeAsync(AgentRuntimeRequest request, CancellationToken ct = default) =>
        await (await ResolveAsync(ct)).InvokeAsync(request, ct);

    public async IAsyncEnumerable<string> StreamAsync(AgentRuntimeRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var runtime = await ResolveAsync(ct);
        await foreach (var chunk in runtime.StreamAsync(request, ct))
            yield return chunk;
    }

    private async Task<IAgentRuntime> ResolveAsync(CancellationToken ct)
    {
        if (_currentUser.TenantId is not { } tenantId)
            return _semanticKernelRuntime;

        var kind = await _policy.GetRuntimeAsync(tenantId, ct);
        return kind == AgentRuntimeKind.AgentFramework ? _agentFrameworkRuntime : _semanticKernelRuntime;
    }
}
