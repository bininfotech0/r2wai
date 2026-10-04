using System.Linq.Expressions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

public class KnowledgePolicyServiceTests
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
    public async Task GetMaxClassificationAsync_NoPolicyConfigured_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var service = new KnowledgePolicyService(new FakeRepository());

        Assert.Null(await service.GetMaxClassificationAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxClassificationAsync_ProsePolicyContent_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Knowledge", "Knowledge rule", "Describe the rule...", isActive: true);
        var service = new KnowledgePolicyService(new FakeRepository(policy));

        Assert.Null(await service.GetMaxClassificationAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxClassificationAsync_StructuredPolicyContent_ReturnsConfiguredCeiling()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Knowledge", "Knowledge rule", "{\"maxClassification\":\"Internal\"}", isActive: true);
        var service = new KnowledgePolicyService(new FakeRepository(policy));

        Assert.Equal("Internal", await service.GetMaxClassificationAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxClassificationAsync_InactivePolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Knowledge", "Knowledge rule", "{\"maxClassification\":\"Public\"}", isActive: false);
        var service = new KnowledgePolicyService(new FakeRepository(policy));

        Assert.Null(await service.GetMaxClassificationAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxClassificationAsync_OtherTenantsPolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), otherTenantId, "Knowledge", "Knowledge rule", "{\"maxClassification\":\"Public\"}", isActive: true);
        var service = new KnowledgePolicyService(new FakeRepository(policy));

        Assert.Null(await service.GetMaxClassificationAsync(tenantId));
    }
}
