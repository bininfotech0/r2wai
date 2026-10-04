using System.Linq.Expressions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

public class PiiPolicyServiceTests
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

    private const string Message = "My Aadhaar is 123456789012, please help.";

    [Fact]
    public async Task CheckAsync_NoPolicyConfigured_PassesThroughUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var service = new PiiPolicyService(new FakeRepository());

        var result = await service.CheckAsync(Message, tenantId);

        Assert.False(result.Blocked);
        Assert.Equal(Message, result.ProcessedText);
        Assert.Empty(result.DetectedTypes);
    }

    [Fact]
    public async Task CheckAsync_ProsePolicyContent_PassesThroughUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Pii", "PII rule", "Describe the rule...", isActive: true);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync(Message, tenantId);

        Assert.False(result.Blocked);
        Assert.Equal(Message, result.ProcessedText);
    }

    [Fact]
    public async Task CheckAsync_RedactAction_NoPiiInText_PassesThroughUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Pii", "PII rule", "{\"action\":\"redact\"}", isActive: true);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync("Nothing sensitive here.", tenantId);

        Assert.False(result.Blocked);
        Assert.Equal("Nothing sensitive here.", result.ProcessedText);
        Assert.Empty(result.DetectedTypes);
    }

    [Fact]
    public async Task CheckAsync_RedactAction_PiiFound_ReturnsRedactedText()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Pii", "PII rule", "{\"action\":\"redact\"}", isActive: true);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync(Message, tenantId);

        Assert.False(result.Blocked);
        Assert.DoesNotContain("123456789012", result.ProcessedText);
        Assert.Contains("Aadhaar", result.DetectedTypes);
    }

    [Fact]
    public async Task CheckAsync_BlockAction_PiiFound_ReturnsBlockedWithOriginalText()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Pii", "PII rule", "{\"action\":\"block\"}", isActive: true);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync(Message, tenantId);

        Assert.True(result.Blocked);
        Assert.Contains("Aadhaar", result.DetectedTypes);
    }

    [Fact]
    public async Task CheckAsync_InactivePolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Pii", "PII rule", "{\"action\":\"block\"}", isActive: false);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync(Message, tenantId);

        Assert.False(result.Blocked);
        Assert.Equal(Message, result.ProcessedText);
    }

    [Fact]
    public async Task CheckAsync_OtherTenantsPolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), otherTenantId, "Pii", "PII rule", "{\"action\":\"block\"}", isActive: true);
        var service = new PiiPolicyService(new FakeRepository(policy));

        var result = await service.CheckAsync(Message, tenantId);

        Assert.False(result.Blocked);
        Assert.Equal(Message, result.ProcessedText);
    }
}
