using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// P0-5 (2026-09-20 audit): ApplicationDbContext's tenant query filter was
/// `TenantId == null || EF.Property&lt;Guid&gt;(e, "TenantId") == TenantId` — when the ambient tenant
/// (ICurrentUserService.TenantId, from the JWT's tenant_id claim) is null, that filter matches EVERY
/// tenant's rows, not zero. Investigation this session found every currently-known path that can
/// legitimately have a null ambient tenant already bypasses the filter explicitly
/// (.IgnoreQueryFilters(), used by every background sweeper) or never reaches it (every standard
/// login always issues a tenant_id claim) — but the fail-open branch itself remained a live risk for
/// any *future* authenticated-but-tenant-missing path (a bug, a new SSO flow, a service account).
/// This proves the fail-closed fix directly against a real DbContext, not by re-deriving the reasoning
/// — the actual mechanism under test, same as every other regression test in this project.
/// </summary>
public class TenantIsolationFailClosedTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private readonly R2WAIWebApplicationFactory _factory;

    public TenantIsolationFailClosedTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    private sealed class Harness(IServiceScope scope) : IDisposable
    {
        public IServiceScope Scope { get; } = scope;
        public ApplicationDbContext Context => Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public void Dispose() => Scope.Dispose();
    }

    // No tenant_id claim at all — the scenario the fail-open branch was meant to (mis)handle. A real
    // anonymous request has no ClaimsPrincipal in the first place; this simulates the closer, scarier
    // case: an authenticated-looking principal whose token is missing the claim for some reason (a
    // bug, a non-standard SSO flow, a service account) — CurrentUserService.TenantId returns null
    // exactly the same way either way, so this exercises the real code path precisely.
    private Harness SignInWithNoTenantClaim()
    {
        var scope = _factory.Services.CreateScope();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new Harness(scope);
    }

    private Harness SignIn(Guid tenantId)
    {
        var scope = _factory.Services.CreateScope();
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new Harness(scope);
    }

    private async Task<(Guid TenantAId, Guid TenantBId)> SeedTwoTenantsKnowledgeBaseAsync()
    {
        using var seedScope = _factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        db.KnowledgeBases.Add(new KnowledgeBase(Guid.NewGuid(), tenantAId, Guid.NewGuid(), "Tenant A's KB"));
        db.KnowledgeBases.Add(new KnowledgeBase(Guid.NewGuid(), tenantBId, Guid.NewGuid(), "Tenant B's KB"));
        await db.SaveChangesAsync();

        return (tenantAId, tenantBId);
    }

    [Fact]
    public async Task NoAmbientTenant_QueryingATenantScopedEntity_ReturnsNoRows_NotEveryTenants()
    {
        var (tenantAId, tenantBId) = await SeedTwoTenantsKnowledgeBaseAsync();

        using var harness = SignInWithNoTenantClaim();
        var rows = await harness.Context.KnowledgeBases
            .Where(k => k.TenantId == tenantAId || k.TenantId == tenantBId)
            .ToListAsync();

        // The bug this guards against: the old filter (`TenantId == null || ...`) matched everything
        // once the ambient tenant was null, so this would have returned both seeded rows instead of
        // none.
        Assert.Empty(rows);
    }

    [Fact]
    public async Task AmbientTenantSet_StillOnlySeesItsOwnTenantsRows()
    {
        var (tenantAId, tenantBId) = await SeedTwoTenantsKnowledgeBaseAsync();

        using var harness = SignIn(tenantAId);
        var rows = await harness.Context.KnowledgeBases
            .Where(k => k.TenantId == tenantAId || k.TenantId == tenantBId)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(tenantAId, rows[0].TenantId);
    }

    [Fact]
    public async Task NoAmbientTenant_ExplicitIgnoreQueryFilters_StillSeesEveryTenantsRows()
    {
        // The escape hatch every background sweeper already uses (P0-3, same session) must keep
        // working unchanged — fail-closed only changes the *implicit* ambient-filter behavior, not
        // this explicit, deliberate bypass.
        var (tenantAId, tenantBId) = await SeedTwoTenantsKnowledgeBaseAsync();

        using var harness = SignInWithNoTenantClaim();
        var rows = await harness.Context.KnowledgeBases
            .IgnoreQueryFilters()
            .Where(k => k.TenantId == tenantAId || k.TenantId == tenantBId)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
    }
}
