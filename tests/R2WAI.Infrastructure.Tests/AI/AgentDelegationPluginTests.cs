using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.Plugins;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentDelegationPlugin — real multi-agent delegation built as "another governed tool
/// call" rather than a parallel orchestration engine (see the class's own doc comment). The generic
/// governance (role/risk/approval) is already proven by ToolGovernanceFilterTests for every built-in
/// plugin uniformly; this covers what's specific to delegation: the per-target allow-list check,
/// the depth cap, and parallel fan-out aggregation.
/// </summary>
public class AgentDelegationPluginTests
{
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; } = Guid.NewGuid();
        public string[] Roles { get; set; } = [];
        public bool IsAuthenticated => true;
        public string? IpAddress => null;
        public string? CorrelationId => null;
    }

    private sealed class FakeAssistantRepository(params AssistantDefinition[] rows) : R2WAI.Domain.Interfaces.IRepository<AssistantDefinition>
    {
        private readonly List<AssistantDefinition> _rows = rows.ToList();
        public Task<AssistantDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(a => a.Id == id));
        public Task<AssistantDefinition?> GetByIdAsync(Guid id, System.Linq.Expressions.Expression<Func<AssistantDefinition, object>> include, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<AssistantDefinition?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<IReadOnlyList<AssistantDefinition>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<AssistantDefinition>)_rows);
        public Task<IReadOnlyList<AssistantDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<AssistantDefinition>)_rows.AsQueryable().Where(predicate).ToList());
        public Task<IReadOnlyList<AssistantDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>> predicate, string includePath, CancellationToken ct = default) =>
            FindAsync(predicate, ct);
        public Task<AssistantDefinition?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<AssistantDefinition> AddAsync(AssistantDefinition entity, CancellationToken ct = default) { _rows.Add(entity); return Task.FromResult(entity); }
        public void Update(AssistantDefinition entity) { }
        public void Delete(AssistantDefinition entity) => _rows.Remove(entity);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.Any(a => a.Id == id));
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Count(predicate));
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private static AssistantDefinition CreateActiveAssistant(Guid tenantId, string name)
    {
        var assistant = new AssistantDefinition(Guid.NewGuid(), tenantId, name, AssistantType.General);
        assistant.Publish();
        return assistant;
    }

    private static (AgentDelegationPlugin Plugin, EnabledToolScope Scope, Mock<IAgentRuntime> RuntimeMock) CreateSut(
        FakeCurrentUser currentUser, params AssistantDefinition[] assistants)
    {
        var scope = new EnabledToolScope();
        var runtimeMock = new Mock<IAgentRuntime>();
        runtimeMock
            .Setup(r => r.InvokeAsync(It.IsAny<AgentRuntimeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRuntimeRequest req, CancellationToken _) =>
                new AgentRuntimeResult($"handled: {req.Message}", Guid.NewGuid(), []));

        // AgentDelegationPlugin resolves IRepository<AssistantDefinition>/IAgentRuntime from a
        // fresh IServiceScopeFactory.CreateScope() per delegation (see its own comment on why:
        // parallel delegate_to_agents branches must not share one scoped DbContext). A real
        // ServiceCollection, not a bare mock, so CreateScope() behaves like the genuine article.
        var services = new ServiceCollection();
        services.AddSingleton<IRepository<AssistantDefinition>>(new FakeAssistantRepository(assistants));
        services.AddSingleton(runtimeMock.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var plugin = new AgentDelegationPlugin(currentUser, scope, scopeFactory, NullLogger<AgentDelegationPlugin>.Instance);

        return (plugin, scope, runtimeMock);
    }

    [Fact]
    public async Task DelegateToAgent_TargetNotInAllowList_IsDenied_AndNeverInvokesTheRuntime()
    {
        var currentUser = new FakeCurrentUser();
        var target = CreateActiveAssistant(currentUser.TenantId!.Value, "Finance Agent");
        var (plugin, scope, runtimeMock) = CreateSut(currentUser, target);
        scope.DelegatableAssistantIds = []; // not allow-listed

        var result = await plugin.DelegateToAgentAsync(target.Id, "What's the Q3 budget?");

        Assert.Contains("not configured to delegate", result);
        runtimeMock.Verify(r => r.InvokeAsync(It.IsAny<AgentRuntimeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DelegateToAgent_AllowListedActiveTarget_InvokesTheRuntimeWithTheTargetsOwnPromptAndTools()
    {
        var currentUser = new FakeCurrentUser();
        var target = CreateActiveAssistant(currentUser.TenantId!.Value, "Finance Agent");
        target.UpdateDetails("Finance Agent", null, "You are a finance specialist.",
            tools: "[]", settings: null);
        var (plugin, scope, runtimeMock) = CreateSut(currentUser, target);
        scope.DelegatableAssistantIds = [target.Id];

        var result = await plugin.DelegateToAgentAsync(target.Id, "What's the Q3 budget?");

        Assert.Equal("handled: What's the Q3 budget?", result);
        runtimeMock.Verify(r => r.InvokeAsync(
            It.Is<AgentRuntimeRequest>(req => req.SystemPrompt == "You are a finance specialist." && req.Message == "What's the Q3 budget?"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DelegateToAgent_TargetBelongsToAnotherTenant_IsTreatedAsUnavailable()
    {
        var currentUser = new FakeCurrentUser();
        var otherTenantTarget = CreateActiveAssistant(Guid.NewGuid(), "Someone Else's Agent");
        var (plugin, scope, runtimeMock) = CreateSut(currentUser, otherTenantTarget);
        scope.DelegatableAssistantIds = [otherTenantTarget.Id];

        var result = await plugin.DelegateToAgentAsync(otherTenantTarget.Id, "task");

        Assert.Contains("no longer exists or is inactive", result);
        runtimeMock.Verify(r => r.InvokeAsync(It.IsAny<AgentRuntimeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DelegateToAgents_FansOutInParallel_AndAggregatesEachLabeledByAssistantName()
    {
        var currentUser = new FakeCurrentUser();
        var financeAgent = CreateActiveAssistant(currentUser.TenantId!.Value, "Finance Agent");
        var hrAgent = CreateActiveAssistant(currentUser.TenantId!.Value, "HR Agent");
        var (plugin, scope, _) = CreateSut(currentUser, financeAgent, hrAgent);
        scope.DelegatableAssistantIds = [financeAgent.Id, hrAgent.Id];

        var tasksJson = $$"""[{"assistantId":"{{financeAgent.Id}}","task":"budget?"},{"assistantId":"{{hrAgent.Id}}","task":"headcount?"}]""";
        var result = await plugin.DelegateToAgentsAsync(tasksJson);

        Assert.Contains("--- Finance Agent ---", result);
        Assert.Contains("handled: budget?", result);
        Assert.Contains("--- HR Agent ---", result);
        Assert.Contains("handled: headcount?", result);
    }

    [Fact]
    public async Task DelegateToAgents_InvalidJson_ReturnsAnErrorInsteadOfThrowing()
    {
        var currentUser = new FakeCurrentUser();
        var (plugin, _, _) = CreateSut(currentUser);

        var result = await plugin.DelegateToAgentsAsync("not json");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task DelegateToAgent_BeyondMaxDepth_IsDenied_WithoutInvokingTheRuntime()
    {
        var currentUser = new FakeCurrentUser();
        var target = CreateActiveAssistant(currentUser.TenantId!.Value, "Finance Agent");
        var (plugin, scope, runtimeMock) = CreateSut(currentUser, target);
        scope.DelegatableAssistantIds = [target.Id];

        // Simulate already being MaxDepth deep (e.g. nested inside other delegations on this
        // same logical call stack) before this call is attempted.
        using var d1 = DelegationDepth.TryEnter();
        using var d2 = DelegationDepth.TryEnter();
        using var d3 = DelegationDepth.TryEnter();
        Assert.NotNull(d1); Assert.NotNull(d2); Assert.NotNull(d3);

        var result = await plugin.DelegateToAgentAsync(target.Id, "task");

        Assert.Contains("maximum delegation depth", result);
        runtimeMock.Verify(r => r.InvokeAsync(It.IsAny<AgentRuntimeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
