using System.Linq.Expressions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Prompts;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers PromptTemplateService against fake IRepository/IUnitOfWork implementations (this project
/// doesn't use a real EF provider — see PostgresIntegrationTests in R2WAI.Api.Tests for that). Focus
/// is the fallback-to-static-default behavior and the version/supersede semantics on edit.
/// </summary>
public class PromptTemplateServiceTests
{
    private sealed class FakeRepository<T> : IRepository<T> where T : R2WAI.Domain.Common.BaseEntity<Guid>
    {
        public List<T> Items { get; } = [];

        public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<T?> GetByIdAsync(Guid id, Expression<Func<T, object>> include, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<T?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<T>)Items.ToList());

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<T>)Items.AsQueryable().Where(predicate).ToList());

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, string includePath, CancellationToken ct = default) =>
            FindAsync(predicate, ct);

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(Items.AsQueryable().Where(predicate).FirstOrDefault());

        public Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(T entity) { }
        public void Delete(T entity) => Items.Remove(entity);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default) => await operation();
    }

    [Fact]
    public async Task GetActiveTemplateAsync_NoOverride_ReturnsStaticDefault()
    {
        var service = new PromptTemplateService(new FakeRepository<PromptTemplate>(), new FakeUnitOfWork());

        var result = await service.GetActiveTemplateAsync(AssistantType.HR, Guid.NewGuid());

        Assert.Equal(SystemPromptTemplates.GetTemplate(AssistantType.HR), result);
    }

    [Fact]
    public async Task SetTemplateAsync_ThenGetActiveTemplateAsync_ReturnsOverride()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.HR, tenantId, "Custom HR wording", CancellationToken.None);
        var result = await service.GetActiveTemplateAsync(AssistantType.HR, tenantId);

        Assert.Equal("Custom HR wording", result);
    }

    [Fact]
    public async Task SetTemplateAsync_DoesNotAffectOtherTenants()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.HR, tenantA, "Tenant A override", CancellationToken.None);
        var resultB = await service.GetActiveTemplateAsync(AssistantType.HR, tenantB);

        Assert.Equal(SystemPromptTemplates.GetTemplate(AssistantType.HR), resultB);
    }

    [Fact]
    public async Task SetTemplateAsync_CalledTwice_SupersedesPriorVersion_OnlyOneActive()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.HR, tenantId, "v1", CancellationToken.None);
        await service.SetTemplateAsync(AssistantType.HR, tenantId, "v2", CancellationToken.None);

        var activeRows = repo.Items.Where(t => t.TenantId == tenantId && t.IsActive).ToList();
        Assert.Single(activeRows);
        Assert.Equal("v2", activeRows[0].Content);
        Assert.Equal(2, activeRows[0].Version);

        var result = await service.GetActiveTemplateAsync(AssistantType.HR, tenantId);
        Assert.Equal("v2", result);
    }

    [Fact]
    public async Task ResetTemplateAsync_WithAnActiveOverride_SupersedesIt_FallsBackToStaticDefault()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.HR, tenantId, "Custom HR wording", CancellationToken.None);
        var wasReset = await service.ResetTemplateAsync(AssistantType.HR, tenantId, CancellationToken.None);

        Assert.True(wasReset);
        var result = await service.GetActiveTemplateAsync(AssistantType.HR, tenantId);
        Assert.Equal(SystemPromptTemplates.GetTemplate(AssistantType.HR), result);
        // The superseded row stays for history, same as an edit — it's just no longer active.
        Assert.Single(repo.Items, t => t.TenantId == tenantId && !t.IsActive && t.Content == "Custom HR wording");
    }

    [Fact]
    public async Task ResetTemplateAsync_WithNoActiveOverride_IsANoOp_ReturnsFalse()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());

        var wasReset = await service.ResetTemplateAsync(AssistantType.HR, Guid.NewGuid(), CancellationToken.None);

        Assert.False(wasReset);
    }

    [Fact]
    public async Task ResetTemplateAsync_DoesNotAffectOtherTenants()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.HR, tenantA, "Tenant A override", CancellationToken.None);
        await service.SetTemplateAsync(AssistantType.HR, tenantB, "Tenant B override", CancellationToken.None);

        await service.ResetTemplateAsync(AssistantType.HR, tenantA, CancellationToken.None);
        var resultB = await service.GetActiveTemplateAsync(AssistantType.HR, tenantB);

        Assert.Equal("Tenant B override", resultB);
    }

    [Fact]
    public async Task GetAllActiveTemplatesAsync_MergesOverrideOverStaticDefaults()
    {
        var repo = new FakeRepository<PromptTemplate>();
        var service = new PromptTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync(AssistantType.IT, tenantId, "Custom IT wording", CancellationToken.None);
        var all = await service.GetAllActiveTemplatesAsync(tenantId);

        Assert.Equal("Custom IT wording", all["IT"]);
        Assert.Equal(SystemPromptTemplates.GetTemplate(AssistantType.HR), all["HR"]); // untouched type stays default
    }
}
