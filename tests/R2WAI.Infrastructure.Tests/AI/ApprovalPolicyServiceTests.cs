using System.Linq.Expressions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

public class ApprovalPolicyServiceTests
{
    private sealed class FakeRepository : IRepository<GlobalPolicy>
    {
        private readonly List<GlobalPolicy> _items;
        public FakeRepository(params GlobalPolicy[] items) => _items = items.ToList();

        public Task<GlobalPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(p => p.Id == id));
        public Task<GlobalPolicy?> GetByIdAsync(Guid id, Expression<Func<GlobalPolicy, object>> include, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<GlobalPolicy?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<IReadOnlyList<GlobalPolicy>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GlobalPolicy>>(_items);
        public Task<IReadOnlyList<GlobalPolicy>> FindAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GlobalPolicy>>(_items.Where(predicate.Compile()).ToList());
        public Task<IReadOnlyList<GlobalPolicy>> FindAsync(Expression<Func<GlobalPolicy, bool>> predicate, string includePath, CancellationToken ct = default) =>
            FindAsync(predicate, ct);
        public Task<GlobalPolicy?> FirstOrDefaultAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<GlobalPolicy> AddAsync(GlobalPolicy entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(GlobalPolicy entity) => throw new NotImplementedException();
        public void Delete(GlobalPolicy entity) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<GlobalPolicy, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task GetRequireApprovalAboveRiskLevelAsync_NoPolicyConfigured_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var service = new ApprovalPolicyService(new FakeRepository());

        Assert.Null(await service.GetRequireApprovalAboveRiskLevelAsync(tenantId));
    }

    [Fact]
    public async Task GetRequireApprovalAboveRiskLevelAsync_StructuredPolicyContent_ReturnsConfiguredFloor()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Approval", "Approval rule", "{\"requireApprovalAboveRiskLevel\":\"Low\"}", isActive: true);
        var service = new ApprovalPolicyService(new FakeRepository(policy));

        Assert.Equal("Low", await service.GetRequireApprovalAboveRiskLevelAsync(tenantId));
    }

    [Fact]
    public async Task GetRequireApprovalAboveRiskLevelAsync_InactivePolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Approval", "Approval rule", "{\"requireApprovalAboveRiskLevel\":\"Low\"}", isActive: false);
        var service = new ApprovalPolicyService(new FakeRepository(policy));

        Assert.Null(await service.GetRequireApprovalAboveRiskLevelAsync(tenantId));
    }

    [Fact]
    public async Task GetRequireApprovalAboveRiskLevelAsync_OtherTenantsPolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), otherTenantId, "Approval", "Approval rule", "{\"requireApprovalAboveRiskLevel\":\"Low\"}", isActive: true);
        var service = new ApprovalPolicyService(new FakeRepository(policy));

        Assert.Null(await service.GetRequireApprovalAboveRiskLevelAsync(tenantId));
    }
}
