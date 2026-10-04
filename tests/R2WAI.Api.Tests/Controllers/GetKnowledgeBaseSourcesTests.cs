using MediatR;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Models;
using R2WAI.Application.Features.KnowledgeBases.DTOs;
using R2WAI.Application.Features.KnowledgeBases.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.2 #57 — the new flat, tenant-wide source list.
/// KnowledgeBaseSource has no TenantId column, so this is the same class of tenant-isolation
/// risk P0-5's fallout sweep found repeatedly elsewhere: tenant scoping is enforced explicitly
/// through the parent KnowledgeBase, not the ambient global filter, and that needs a real test,
/// not just a build check.
/// </summary>
public class GetKnowledgeBaseSourcesTests : IntegrationTestBase
{
    public GetKnowledgeBaseSourcesTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<(Guid KbId, Guid SourceId)> SeedKbWithSourceAsync(Guid tenantId, string kbName, string? status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var kb = new KnowledgeBase(Guid.NewGuid(), tenantId, Guid.NewGuid(), kbName);
        db.KnowledgeBases.Add(kb);
        var source = new KnowledgeBaseSource(Guid.NewGuid(), kb.Id, "text", content: "hello");
        if (status is not null) source.UpdateStatus(status);
        db.KnowledgeBaseSources.Add(source);
        await db.SaveChangesAsync();
        return (kb.Id, source.Id);
    }

    [Fact]
    public async Task GetSources_OnlyReturnsTheCallersTenant_NeverAnotherTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (_, sourceA) = await SeedKbWithSourceAsync(tenantA, "Tenant A KB", "Indexed");
        var (_, sourceB) = await SeedKbWithSourceAsync(tenantB, "Tenant B KB", "Indexed");

        using var scope = SignedInScope(tenantA, Guid.NewGuid());
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetKnowledgeBaseSourcesQuery());

        Assert.Contains(result.Items, s => s.Id == sourceA);
        Assert.DoesNotContain(result.Items, s => s.Id == sourceB);
    }

    [Fact]
    public async Task GetSources_StatusFilter_NarrowsToMatchingStatusOnly()
    {
        var tenantId = Guid.NewGuid();
        var (_, indexedId) = await SeedKbWithSourceAsync(tenantId, "Indexed KB", "Indexed");
        var (_, failedId) = await SeedKbWithSourceAsync(tenantId, "Failed KB", "Failed");

        using var scope = SignedInScope(tenantId, Guid.NewGuid());
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetKnowledgeBaseSourcesQuery { Status = "Failed" });

        Assert.Contains(result.Items, s => s.Id == failedId);
        Assert.DoesNotContain(result.Items, s => s.Id == indexedId);
    }

    [Fact]
    public async Task GetSources_IncludesKnowledgeBaseIdentityForContext()
    {
        var tenantId = Guid.NewGuid();
        var (kbId, sourceId) = await SeedKbWithSourceAsync(tenantId, "Named KB", "Indexed");

        using var scope = SignedInScope(tenantId, Guid.NewGuid());
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetKnowledgeBaseSourcesQuery());

        var item = Assert.Single(result.Items, s => s.Id == sourceId);
        Assert.Equal(kbId, item.KnowledgeBaseId);
        Assert.Equal("Named KB", item.KnowledgeBaseName);
    }

    [Fact]
    public async Task GetSources_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/v1/knowledgebases/sources");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
