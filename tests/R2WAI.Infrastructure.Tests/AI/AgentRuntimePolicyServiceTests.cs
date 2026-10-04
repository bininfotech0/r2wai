using System.Linq.Expressions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentRuntimePolicyService — the per-tenant SK-vs-MAF flag (implementation plan Phase 4),
/// backed by the same GlobalPolicy table ToolExecutionPolicyService/ApprovalPolicyService already
/// use. The property under test: an absent, inactive, or unparseable policy row all fail closed to
/// SemanticKernel (the proven runtime), never to AgentFramework.
/// </summary>
public class AgentRuntimePolicyServiceTests
{
    private sealed class FakePolicyRepository : IRepository<GlobalPolicy>
    {
        private readonly List<GlobalPolicy> _rows;
        public FakePolicyRepository(params GlobalPolicy[] rows) => _rows = rows.ToList();

        public Task<GlobalPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.FirstOrDefault(p => p.Id == id));
        public Task<GlobalPolicy?> GetByIdAsync(Guid id, Expression<Func<GlobalPolicy, object>> include, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<GlobalPolicy?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<GlobalPolicy>> GetAllAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<GlobalPolicy>)_rows);
        public Task<IReadOnlyList<GlobalPolicy>> FindAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<GlobalPolicy>)_rows.AsQueryable().Where(predicate).ToList());
        public Task<IReadOnlyList<GlobalPolicy>> FindAsync(Expression<Func<GlobalPolicy, bool>> predicate, string includePath, CancellationToken ct = default) => FindAsync(predicate, ct);
        public Task<GlobalPolicy?> FirstOrDefaultAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<GlobalPolicy> AddAsync(GlobalPolicy entity, CancellationToken ct = default) { _rows.Add(entity); return Task.FromResult(entity); }
        public void Update(GlobalPolicy entity) { }
        public void Delete(GlobalPolicy entity) => _rows.Remove(entity);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.Any(p => p.Id == id));
        public Task<int> CountAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_rows.AsQueryable().Count(predicate));
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    [Fact]
    public async Task GetRuntimeAsync_NoPolicyRow_DefaultsToSemanticKernel()
    {
        var sut = new AgentRuntimePolicyService(new FakePolicyRepository());

        var kind = await sut.GetRuntimeAsync(Guid.NewGuid());

        Assert.Equal(AgentRuntimeKind.SemanticKernel, kind);
    }

    [Fact]
    public async Task GetRuntimeAsync_ActivePolicySaysAgentFramework_ReturnsAgentFramework()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "AgentRuntime", "Runtime", "AgentFramework", isActive: true);
        var sut = new AgentRuntimePolicyService(new FakePolicyRepository(policy));

        var kind = await sut.GetRuntimeAsync(tenantId);

        Assert.Equal(AgentRuntimeKind.AgentFramework, kind);
    }

    [Fact]
    public async Task GetRuntimeAsync_InactivePolicy_IsIgnored_DefaultsToSemanticKernel()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "AgentRuntime", "Runtime", "AgentFramework", isActive: false);
        var sut = new AgentRuntimePolicyService(new FakePolicyRepository(policy));

        var kind = await sut.GetRuntimeAsync(tenantId);

        Assert.Equal(AgentRuntimeKind.SemanticKernel, kind);
    }

    [Fact]
    public async Task GetRuntimeAsync_UnparseableContent_DefaultsToSemanticKernel_FailsClosedToTheProvenRuntime()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "AgentRuntime", "Runtime", "not-a-real-runtime-name", isActive: true);
        var sut = new AgentRuntimePolicyService(new FakePolicyRepository(policy));

        var kind = await sut.GetRuntimeAsync(tenantId);

        Assert.Equal(AgentRuntimeKind.SemanticKernel, kind);
    }

    [Fact]
    public async Task GetRuntimeAsync_AnotherTenantsPolicy_DoesNotLeakAcross()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantPolicy = new GlobalPolicy(Guid.NewGuid(), Guid.NewGuid(), "AgentRuntime", "Runtime", "AgentFramework", isActive: true);
        var sut = new AgentRuntimePolicyService(new FakePolicyRepository(otherTenantPolicy));

        var kind = await sut.GetRuntimeAsync(tenantId);

        Assert.Equal(AgentRuntimeKind.SemanticKernel, kind);
    }
}
