using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Authentication;

namespace R2WAI.Infrastructure.Tests.Authentication;

public class AuthPolicyServiceTests
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

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public async Task IsMfaRequiredAsync_NoPolicyConfigured_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var service = new AuthPolicyService(new FakeRepository(), Config([]));

        Assert.False(await service.IsMfaRequiredAsync(tenantId));
    }

    [Fact]
    public async Task IsMfaRequiredAsync_ActivePolicyRequiresMfa_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Auth", "MFA required", "{\"requireMfa\":true}", isActive: true);
        var service = new AuthPolicyService(new FakeRepository(policy), Config([]));

        Assert.True(await service.IsMfaRequiredAsync(tenantId));
    }

    [Fact]
    public async Task IsMfaRequiredAsync_InactivePolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Auth", "MFA required", "{\"requireMfa\":true}", isActive: false);
        var service = new AuthPolicyService(new FakeRepository(policy), Config([]));

        Assert.False(await service.IsMfaRequiredAsync(tenantId));
    }

    [Fact]
    public async Task IsMfaRequiredAsync_OtherTenantsPolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), otherTenantId, "Auth", "MFA required", "{\"requireMfa\":true}", isActive: true);
        var service = new AuthPolicyService(new FakeRepository(policy), Config([]));

        Assert.False(await service.IsMfaRequiredAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxPasswordAgeDaysAsync_NoPolicyConfigured_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var service = new AuthPolicyService(new FakeRepository(), Config([]));

        Assert.Null(await service.GetMaxPasswordAgeDaysAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxPasswordAgeDaysAsync_ActivePolicyConfigured_ReturnsValue()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Auth", "Password expiry", "{\"maxPasswordAgeDays\":90}", isActive: true);
        var service = new AuthPolicyService(new FakeRepository(policy), Config([]));

        Assert.Equal(90, await service.GetMaxPasswordAgeDaysAsync(tenantId));
    }

    [Fact]
    public async Task GetMaxPasswordAgeDaysAsync_InactivePolicy_Ignored()
    {
        var tenantId = Guid.NewGuid();
        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, "Auth", "Password expiry", "{\"maxPasswordAgeDays\":90}", isActive: false);
        var service = new AuthPolicyService(new FakeRepository(policy), Config([]));

        Assert.Null(await service.GetMaxPasswordAgeDaysAsync(tenantId));
    }

    [Fact]
    public void IsSsoConfigured_TenantIdAndClientIdSet_ReturnsTrue()
    {
        var service = new AuthPolicyService(new FakeRepository(), Config(new()
        {
            ["Authentication:EntraId:TenantId"] = "tenant-123",
            ["Authentication:EntraId:ClientId"] = "client-456",
        }));

        Assert.True(service.IsSsoConfigured());
    }

    [Theory]
    [InlineData(null, "client-456")]
    [InlineData("tenant-123", null)]
    [InlineData(null, null)]
    public void IsSsoConfigured_MissingTenantOrClientId_ReturnsFalse(string? tenantId, string? clientId)
    {
        var service = new AuthPolicyService(new FakeRepository(), Config(new()
        {
            ["Authentication:EntraId:TenantId"] = tenantId,
            ["Authentication:EntraId:ClientId"] = clientId,
        }));

        Assert.False(service.IsSsoConfigured());
    }
}
