using Microsoft.AspNetCore.Hosting;
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
/// P0-3 (2026-09-20 audit): two API replicas racing the same background sweep window could both
/// process the same overdue row — WorkflowDelayResumeBackgroundService had no claim at all, and
/// ApprovalService.EscalateOverdueAsync's fetch-then-mutate-then-save loop had a TOCTOU window
/// between the two replicas' initial SELECT and their eventual SaveChanges. Neither race is
/// reproducible against the EF Core InMemory provider (single in-process store, no real concurrent
/// transactions), so this needs a real Postgres — same Testcontainers pattern as
/// ChatConcurrencyRegressionTests.cs.
/// </summary>
[Trait("Category", "Integration")]
public class SweeperClaimRegressionTests : IAsyncLifetime
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SeededAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
    public async Task EscalateOverdueAsync_RacedByTwoReplicas_EscalatesExactlyOnce()
    {
        if (!_dockerAvailable) return;

        Guid requestId;
        using (var seedScope = _factory!.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var workflow = new Workflow(Guid.NewGuid(), SeededTenantId, SeededAdminId, "Sweeper claim test workflow");
            db.Workflows.Add(workflow);
            // workflowInstanceId: null — a real FK to WorkflowInstances, unlike the InMemory provider
            // (no FK enforcement) some other tests run against; Step B (DetachApprovalsFromWorkflows)
            // made this nullable specifically so an approval doesn't need a real workflow instance.
            // data: null — ApprovalRequest.Data is a jsonb column; this test doesn't need a payload.
            var overdue = new ApprovalRequest(Guid.NewGuid(), SeededTenantId, workflowInstanceId: null, workflow.Id,
                requesterId: SeededAdminId, data: null, dueAt: DateTime.UtcNow.AddMinutes(-10));
            db.ApprovalRequests.Add(overdue);
            await db.SaveChangesAsync();
            requestId = overdue.Id;
        }

        // Two separate scopes = two separate DbContexts = two "replicas" hitting the same overdue
        // row at (as close as this test can get to) the same instant.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var serviceA = scopeA.ServiceProvider.GetRequiredService<IApprovalService>();
        var serviceB = scopeB.ServiceProvider.GetRequiredService<IApprovalService>();

        await Task.WhenAll(serviceA.EscalateOverdueAsync(), serviceB.EscalateOverdueAsync());

        // IgnoreQueryFilters: this scope has no ambient authenticated HttpContext (P0-5's fail-closed
        // tenant filter, same session) — EscalateOverdueAsync itself already uses IgnoreQueryFilters
        // internally (it's a background sweeper), so only this verification read needs it here.
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await verifyDb.ApprovalRequests.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == requestId);

        Assert.Equal(ApprovalStatus.Escalated, stored.Status);
        // The bug this guards against: without the atomic claim, both replicas load the row while
        // it's still Pending, both call Escalate() (EscalationLevel++), and both save — landing on 2.
        Assert.Equal(1, stored.EscalationLevel);
    }

    [Fact]
    public async Task WorkflowInstanceDelayClaim_RacedByTwoReplicas_ClaimsExactlyOnce()
    {
        if (!_dockerAvailable) return;

        Guid instanceId;
        using (var seedScope = _factory!.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var workflow = new Workflow(Guid.NewGuid(), SeededTenantId, SeededAdminId, "Delay claim test workflow");
            db.Workflows.Add(workflow);
            var instance = new WorkflowInstance(Guid.NewGuid(), workflow.Id, SeededTenantId, SeededAdminId);
            instance.ScheduleDelayedResume(DateTime.UtcNow.AddMinutes(-1), stepIndex: 0);
            db.WorkflowInstances.Add(instance);
            await db.SaveChangesAsync();
            instanceId = instance.Id;
        }

        // Exercises the exact claim query WorkflowDelayResumeBackgroundService.ResumeDueDelaysAsync
        // runs (a conditional UPDATE nulling PendingResumeAt WHERE it's still due), fired from two
        // separate DbContexts concurrently — proves at most one of the two "replicas" wins the row.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Task<int> ClaimAsync(ApplicationDbContext db) => db.WorkflowInstances
            .IgnoreQueryFilters()
            .Where(i => i.Id == instanceId && !i.IsDeleted && i.PendingResumeAt != null && i.PendingResumeAt <= DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.PendingResumeAt, (DateTime?)null));

        var results = await Task.WhenAll(ClaimAsync(dbA), ClaimAsync(dbB));

        // The bug this guards against: without an atomic per-row claim, a plain SELECT-then-process
        // sweep would let both replicas "win" and both call ContinueDelayedWorkflowAsync, running the
        // same remaining workflow steps twice.
        Assert.Equal(1, results.Sum());
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
