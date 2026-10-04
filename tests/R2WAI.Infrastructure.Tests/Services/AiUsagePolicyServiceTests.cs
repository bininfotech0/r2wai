using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Policies;
using R2WAI.Infrastructure.Cache;

namespace R2WAI.Infrastructure.Tests.Services;

// No dedicated coverage existed for this class before — IsUnderCapAsync was only exercised
// indirectly through a Moq'd interface in ChatWithAssistantCommandHandlerTests. Added directly
// alongside GetStatusAsync (new, backs the widget usage card, docs/api/MISSING-BACKEND-ENDPOINTS.md
// §3.1 #54) so the refactor that extracted GetDailyCapAsync has real proof it didn't change
// IsUnderCapAsync's own behavior. Uses the real InMemoryCacheService rather than a fake — the
// counter's actual get/set round-trip through DateTime.UtcNow-keyed cache entries is exactly the
// behavior under test.
public class AiUsagePolicyServiceTests
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

    private static AiUsagePolicyService CreateService(params GlobalPolicy[] policies) =>
        new(new FakeRepository(policies), new InMemoryCacheService(NullLogger<InMemoryCacheService>.Instance));

    [Fact]
    public async Task IsUnderCapAsync_NoPolicyConfigured_ReturnsTrue()
    {
        var service = CreateService();

        Assert.True(await service.IsUnderCapAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task IsUnderCapAsync_UnderTheConfiguredCap_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new GlobalPolicy(Guid.NewGuid(), tenantId, "AiUsage", "Daily cap", "{\"maxRequestsPerDay\":100}", isActive: true));

        Assert.True(await service.IsUnderCapAsync(tenantId));
    }

    [Fact]
    public async Task IsUnderCapAsync_AtTheConfiguredCap_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new GlobalPolicy(Guid.NewGuid(), tenantId, "AiUsage", "Daily cap", "{\"maxRequestsPerDay\":2}", isActive: true));
        await service.RecordRequestAsync(tenantId);
        await service.RecordRequestAsync(tenantId);

        Assert.False(await service.IsUnderCapAsync(tenantId));
    }

    [Fact]
    public async Task GetStatusAsync_NoPolicyConfigured_ReturnsNullCapAndZeroCount()
    {
        var service = CreateService();

        var status = await service.GetStatusAsync(Guid.NewGuid());

        Assert.Null(status.DailyCap);
        Assert.Equal(0, status.CurrentDailyCount);
    }

    [Fact]
    public async Task GetStatusAsync_WithPolicyAndRecordedRequests_ReflectsBoth()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new GlobalPolicy(Guid.NewGuid(), tenantId, "AiUsage", "Daily cap", "{\"maxRequestsPerDay\":50}", isActive: true));
        await service.RecordRequestAsync(tenantId);
        await service.RecordRequestAsync(tenantId);

        var status = await service.GetStatusAsync(tenantId);

        Assert.Equal(50, status.DailyCap);
        Assert.Equal(2, status.CurrentDailyCount);
    }

    [Fact]
    public async Task GetStatusAsync_IsolatedPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var service = CreateService(new GlobalPolicy(Guid.NewGuid(), tenantA, "AiUsage", "Daily cap", "{\"maxRequestsPerDay\":50}", isActive: true));
        await service.RecordRequestAsync(tenantA);

        var statusB = await service.GetStatusAsync(tenantB);

        Assert.Null(statusB.DailyCap);
        Assert.Equal(0, statusB.CurrentDailyCount);
    }
}
