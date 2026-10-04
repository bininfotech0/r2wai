using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// ApprovalService.ApproveAsync/RejectAsync gained an atomic claim (same session as
/// SweeperClaimRegressionTests.cs's sweeper fixes) because approving a request can now trigger a real
/// deferred tool-call execution (DeferredToolCallExecutor) — a double-approve race would mean a
/// non-idempotent write operation actually running twice, not just a duplicate notification email.
/// Not reproducible against the EF Core InMemory provider (no real concurrent transactions), so this
/// needs a real Postgres — same Testcontainers pattern as the other Integration regression tests here.
/// </summary>
[Trait("Category", "Integration")]
public class ApprovalDecisionRaceRegressionTests : IAsyncLifetime
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SeededAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // "deptadmin@r2wai.io" — seeded, Admin role, same tenant, but a different person from
    // SeededAdminId. Approving needs a real distinct approver now that a requester can't decide their
    // own request (see ApprovalService.ApproveAsync's separation-of-duties check).
    private static readonly Guid ApproverUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithDatabase("r2wai_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            _dockerAvailable = false;
            return;
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Authentication:Jwt:SecretKey",
                    "TestingSecretKeyForIntegrationTestsThatIsLongEnough!");
                builder.UseSetting("ConnectionStrings:Redis", "");
                builder.UseSetting("Cache:Redis:ConnectionString", "");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());

                builder.ConfigureServices(services =>
                {
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        d.ServiceType.FullName?.Contains("DbContextOptions") == true ||
                        d.ServiceType == typeof(ApplicationDbContext) ||
                        d.ImplementationType == typeof(ApplicationDbContext) ||
                        d.ServiceType == typeof(ITenantDbContext) ||
                        d.ServiceType == typeof(IHostedService)
                    ).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    var npgsqlDescriptors = services.Where(d =>
                        d.ServiceType.FullName?.Contains("Npgsql") == true ||
                        d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    ).ToList();
                    foreach (var d in npgsqlDescriptors) services.Remove(d);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseNpgsql(_postgres.GetConnectionString());
                        options.ConfigureWarnings(w => w.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });
                    services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                    services.RemoveAll<IEncryptionService>();
                    services.AddSingleton<IEncryptionService, NoOpEncryptionService>();
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task ApproveAsync_RacedByTwoConcurrentCallers_ApprovesExactlyOnce()
    {
        if (!_dockerAvailable) return;

        Guid requestId;
        using (var seedScope = _factory!.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var workflow = new Workflow(Guid.NewGuid(), SeededTenantId, SeededAdminId, "Approve race test workflow");
            db.Workflows.Add(workflow);
            var request = new ApprovalRequest(Guid.NewGuid(), SeededTenantId, workflowInstanceId: null, workflow.Id,
                requesterId: SeededAdminId, data: null);
            db.ApprovalRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
        }

        // ApproveAsync (unlike EscalateOverdueAsync) is not a background-sweeper method — in real use
        // it always runs behind an authenticated HTTP request, so it relies on the ambient tenant
        // filter rather than IgnoreQueryFilters. P0-5's fail-closed change (same session) means these
        // scopes need a real ambient tenant to behave like a genuine authenticated caller.
        using var scopeA = SignedInScope(SeededTenantId, ApproverUserId);
        using var scopeB = SignedInScope(SeededTenantId, ApproverUserId);
        var serviceA = scopeA.ServiceProvider.GetRequiredService<IApprovalService>();
        var serviceB = scopeB.ServiceProvider.GetRequiredService<IApprovalService>();

        // The bug this guards against: without the atomic claim, both callers pass the
        // IsAwaitingDecision check while the row is still Pending, both approve, and — since Approve
        // now can execute a real deferred tool call — a governed write operation would run twice.
        var results = await Task.WhenAll(
            SafeApproveAsync(serviceA, requestId, ApproverUserId),
            SafeApproveAsync(serviceB, requestId, ApproverUserId));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(1, results.Count(r => !r)); // the loser observes the InvalidOperationException, not a silent double-approve

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await verifyDb.ApprovalRequests.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == requestId);
        Assert.Equal(ApprovalStatus.Approved, stored.Status);
    }

    private IServiceScope SignedInScope(Guid tenantId, Guid userId)
    {
        var scope = _factory!.Services.CreateScope();
        var claims = new List<Claim> { new("tenant_id", tenantId.ToString()), new(ClaimTypes.NameIdentifier, userId.ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    private static async Task<bool> SafeApproveAsync(IApprovalService service, Guid requestId, Guid approverId)
    {
        try
        {
            await service.ApproveAsync(requestId, approverId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
