using System.Linq.Expressions;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Workflows;

namespace R2WAI.Infrastructure.Tests.Workflows;

/// <summary>
/// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #82 — the 5 starter workflow templates used to be
/// hardcoded, unreachable-for-editing anonymous objects in WorkflowsController. Mirrors
/// PromptTemplateServiceTests' fake-repository approach (this project has no real EF provider).
/// </summary>
public class WorkflowTemplateServiceTests
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

    private static readonly List<WorkflowTemplateStepDto> SampleSteps =
    [
        new("Step One", "Action", "Requester", 0),
        new("Step Two", "Approval", "Manager", 1),
    ];

    [Fact]
    public async Task GetAllAsync_NoOverrides_ReturnsTheFiveStaticDefaultsUnchanged()
    {
        var service = new WorkflowTemplateService(new FakeRepository<WorkflowTemplateOverride>(), new FakeUnitOfWork());

        var result = await service.GetAllAsync(Guid.NewGuid());

        Assert.Equal(5, result.Count);
        Assert.Equal(WorkflowTemplateDefaults.GetAll().Select(d => d.Id), result.Select(r => r.Id));
    }

    [Fact]
    public async Task SetTemplateAsync_ThenGetAllAsync_ReturnsTheOverride_OtherTemplatesUnaffected()
    {
        var repo = new FakeRepository<WorkflowTemplateOverride>();
        var service = new WorkflowTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync("invoice-approval", tenantId, "My Invoice Flow", "Custom", "Approval", SampleSteps);
        var all = await service.GetAllAsync(tenantId);

        var overridden = all.Single(t => t.Id == "invoice-approval");
        Assert.Equal("My Invoice Flow", overridden.Name);
        Assert.Equal(SampleSteps, overridden.Steps);

        var untouched = all.Single(t => t.Id == "purchase-request");
        Assert.Equal(WorkflowTemplateDefaults.GetAll().Single(d => d.Id == "purchase-request").Name, untouched.Name);
    }

    [Fact]
    public async Task SetTemplateAsync_CalledTwice_UpdatesInPlace_DoesNotCreateASecondRow()
    {
        var repo = new FakeRepository<WorkflowTemplateOverride>();
        var service = new WorkflowTemplateService(repo, new FakeUnitOfWork());
        var tenantId = Guid.NewGuid();

        await service.SetTemplateAsync("invoice-approval", tenantId, "v1", null, "Approval", SampleSteps);
        await service.SetTemplateAsync("invoice-approval", tenantId, "v2", null, "Approval", SampleSteps);

        Assert.Single(repo.Items, o => o.TenantId == tenantId && o.TemplateId == "invoice-approval");
        var result = await service.GetAllAsync(tenantId);
        Assert.Equal("v2", result.Single(t => t.Id == "invoice-approval").Name);
    }

    [Fact]
    public async Task SetTemplateAsync_DoesNotAffectOtherTenants()
    {
        var repo = new FakeRepository<WorkflowTemplateOverride>();
        var service = new WorkflowTemplateService(repo, new FakeUnitOfWork());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await service.SetTemplateAsync("invoice-approval", tenantA, "Tenant A version", null, "Approval", SampleSteps);
        var resultB = await service.GetAllAsync(tenantB);

        Assert.Equal(WorkflowTemplateDefaults.GetAll().Single(d => d.Id == "invoice-approval").Name,
            resultB.Single(t => t.Id == "invoice-approval").Name);
    }

    [Fact]
    public async Task SetTemplateAsync_UnknownTemplateId_ThrowsNotFound()
    {
        var service = new WorkflowTemplateService(new FakeRepository<WorkflowTemplateOverride>(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SetTemplateAsync("not-a-real-template", Guid.NewGuid(), "Name", null, "Approval", SampleSteps));
    }
}
