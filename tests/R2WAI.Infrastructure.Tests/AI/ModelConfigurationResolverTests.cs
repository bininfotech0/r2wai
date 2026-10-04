using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.ModelGateway;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers the "no explicit model chosen" fallback: previously this returned null immediately,
/// which meant every caller that didn't pin an assistant/step to a specific ModelConfiguration
/// silently used the process-wide "AI:Provider" config, completely ignoring a tenant's AI Models
/// catalog and its "Default" toggle. Now it looks up the tenant's IsDefault && IsActive row first.
/// </summary>
public class ModelConfigurationResolverTests
{
    private sealed class FakeEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => $"enc:{plainText}";
        public string Decrypt(string cipherText) => cipherText.StartsWith("enc:") ? cipherText[4..] : cipherText;
    }

    private sealed class FakeRepository : IRepository<ModelConfiguration>
    {
        private readonly List<ModelConfiguration> _items;
        public FakeRepository(params ModelConfiguration[] items) => _items = items.ToList();

        public Task<ModelConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(m => m.Id == id));
        public Task<ModelConfiguration?> GetByIdAsync(Guid id, Expression<Func<ModelConfiguration, object>> include, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<ModelConfiguration?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<IReadOnlyList<ModelConfiguration>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModelConfiguration>>(_items);
        public Task<IReadOnlyList<ModelConfiguration>> FindAsync(Expression<Func<ModelConfiguration, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModelConfiguration>>(_items.Where(predicate.Compile()).ToList());
        public Task<IReadOnlyList<ModelConfiguration>> FindAsync(Expression<Func<ModelConfiguration, bool>> predicate, string includePath, CancellationToken ct = default) =>
            FindAsync(predicate, ct);
        public Task<ModelConfiguration?> FirstOrDefaultAsync(Expression<Func<ModelConfiguration, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<ModelConfiguration> AddAsync(ModelConfiguration entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(ModelConfiguration entity) => throw new NotImplementedException();
        public void Delete(ModelConfiguration entity) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<ModelConfiguration, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static ModelConfiguration MakeConfig(Guid tenantId, string modelId, bool isDefault, bool isActive = true)
    {
        var config = new ModelConfiguration(Guid.NewGuid(), tenantId, modelId, "openai", modelId, apiKeyEncrypted: "enc:secret");
        config.SetDefault(isDefault);
        if (!isActive) config.Deactivate();
        return config;
    }

    [Fact]
    public async Task ResolveAsync_NoExplicitModel_UsesTenantDefault()
    {
        var tenantId = Guid.NewGuid();
        var defaultConfig = MakeConfig(tenantId, "gpt-4o", isDefault: true);
        var repo = new FakeRepository(defaultConfig, MakeConfig(tenantId, "gpt-3.5", isDefault: false));
        var resolver = new ModelConfigurationResolver(repo, new FakeEncryptionService(), NullLogger<ModelConfigurationResolver>.Instance);

        var resolved = await resolver.ResolveAsync(null, tenantId);

        Assert.NotNull(resolved);
        Assert.Equal("gpt-4o", resolved!.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_NoExplicitModel_NoTenantDefault_FallsBackToNull()
    {
        var tenantId = Guid.NewGuid();
        var repo = new FakeRepository(MakeConfig(tenantId, "gpt-3.5", isDefault: false));
        var resolver = new ModelConfigurationResolver(repo, new FakeEncryptionService(), NullLogger<ModelConfigurationResolver>.Instance);

        var resolved = await resolver.ResolveAsync(null, tenantId);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_NoExplicitModel_IgnoresInactiveDefault()
    {
        var tenantId = Guid.NewGuid();
        var repo = new FakeRepository(MakeConfig(tenantId, "gpt-4o", isDefault: true, isActive: false));
        var resolver = new ModelConfigurationResolver(repo, new FakeEncryptionService(), NullLogger<ModelConfigurationResolver>.Instance);

        var resolved = await resolver.ResolveAsync(null, tenantId);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_NoExplicitModel_IgnoresOtherTenantsDefault()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var repo = new FakeRepository(MakeConfig(otherTenantId, "gpt-4o", isDefault: true));
        var resolver = new ModelConfigurationResolver(repo, new FakeEncryptionService(), NullLogger<ModelConfigurationResolver>.Instance);

        var resolved = await resolver.ResolveAsync(null, tenantId);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_ExplicitModelId_StillResolvesDirectly_UnaffectedByDefaultFallback()
    {
        var tenantId = Guid.NewGuid();
        var explicitConfig = MakeConfig(tenantId, "claude-opus", isDefault: false);
        var repo = new FakeRepository(explicitConfig, MakeConfig(tenantId, "gpt-4o", isDefault: true));
        var resolver = new ModelConfigurationResolver(repo, new FakeEncryptionService(), NullLogger<ModelConfigurationResolver>.Instance);

        var resolved = await resolver.ResolveAsync(explicitConfig.Id, tenantId);

        Assert.NotNull(resolved);
        Assert.Equal("claude-opus", resolved!.ModelId);
    }
}
