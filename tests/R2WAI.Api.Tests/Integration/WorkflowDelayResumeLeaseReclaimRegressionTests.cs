using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Implementation plan Phase 5's crash-simulation regression test for
/// WorkflowDelayResumeBackgroundService — the same shape of bug as
/// BackgroundJobLeaseReclaimRegressionTests, in WorkflowInstance's delay-resume state instead of
/// BackgroundJob's Status: a replica that claimed a due delayed instance and then crashed before
/// ContinueDelayedWorkflowAsync finished used to leave PendingResumeAt cleared with
/// PendingResumeStepIndex still set and Status still Running — indistinguishable from a normal
/// in-progress instance, abandoned forever with no error.
///
/// IWorkflowBridge resolves to NoOpWorkflowBridge in the "Testing" environment this harness runs
/// under (a documented pitfall elsewhere in this codebase) — it never calls ClearPendingResume(),
/// so it can't stand in for "a real resume completed". This test replaces it with its own minimal
/// fake that does call ClearPendingResume(), the one thing this test needs to observe: did the
/// sweep's claim actually reach IWorkflowBridge.ContinueDelayedWorkflowAsync.
/// </summary>
[Trait("Category", "Integration")]
public class WorkflowDelayResumeLeaseReclaimRegressionTests : IAsyncLifetime
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
                    // Keep IHostedService registrations — WorkflowDelayResumeBackgroundService
                    // actually running for real, on its own poll loop, is the point of this test.
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        d.ServiceType.FullName?.Contains("DbContextOptions") == true ||
                        d.ServiceType == typeof(ApplicationDbContext) ||
                        d.ImplementationType == typeof(ApplicationDbContext) ||
                        d.ServiceType == typeof(ITenantDbContext)
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

                    // Replaces NoOpWorkflowBridge — see this class's own doc comment.
                    services.RemoveAll<IWorkflowBridge>();
                    services.AddScoped<IWorkflowBridge, RecordingWorkflowBridge>();
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);

        _factory.CreateClient(); // forces IHostedServices to actually start
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private async Task<Guid> SeedDueInstanceAsync(DateTime? leaseExpiresAt)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var workflow = new Workflow(Guid.NewGuid(), SeededTenantId, SeededAdminId, $"Lease Test {Guid.NewGuid():N}");
        db.Set<Workflow>().Add(workflow);

        var instance = new WorkflowInstance(Guid.NewGuid(), workflow.Id, SeededTenantId, SeededAdminId);
        instance.ScheduleDelayedResume(DateTime.UtcNow.AddMinutes(-1), stepIndex: 2); // already due
        db.Set<WorkflowInstance>().Add(instance);
        await db.SaveChangesAsync();

        if (leaseExpiresAt is not null)
        {
            // IgnoreQueryFilters: this raw scope has no ambient authenticated HttpContext — the
            // fail-closed tenant filter would otherwise match zero rows regardless of Id.
            await db.Set<WorkflowInstance>()
                .IgnoreQueryFilters()
                .Where(i => i.Id == instance.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.PendingResumeLeaseExpiresAt, leaseExpiresAt));
        }

        return instance.Id;
    }

    [Fact]
    public async Task AbandonedLease_IsReclaimedAfterExpiry_AndResumedExactlyOnce()
    {
        if (!_dockerAvailable) return;

        // Stand-in for "a replica claimed this and crashed 15 minutes ago": lease already expired,
        // PendingResumeAt still set (this fix's whole point — the old code cleared it on claim).
        var instanceId = await SeedDueInstanceAsync(DateTime.UtcNow.AddMinutes(-15));

        WorkflowInstance? finalState = null;
        var deadline = DateTime.UtcNow.AddSeconds(75); // sweep polls every 30s
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000);
            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var instance = await db.Set<WorkflowInstance>().IgnoreQueryFilters().FirstAsync(i => i.Id == instanceId);
            if (instance.PendingResumeAt is null)
            {
                finalState = instance;
                break;
            }
        }

        Assert.NotNull(finalState);
        Assert.Null(finalState!.PendingResumeStepIndex);
        Assert.Null(finalState.PendingResumeLeaseExpiresAt);
        Assert.Equal(1, RecordingWorkflowBridge.ContinueCallCounts.GetValueOrDefault(instanceId));
    }

    [Fact]
    public async Task ActivelyLeasedInstance_BeforeExpiry_IsNotReclaimed()
    {
        if (!_dockerAvailable) return;

        // Negative control: an unexpired lease must block reclaim — proves this is a real expiry
        // check, not "reclaim every due instance regardless of an in-flight claim."
        var instanceId = await SeedDueInstanceAsync(DateTime.UtcNow.AddMinutes(10));

        await Task.Delay(TimeSpan.FromSeconds(35)); // let at least one real poll cycle pass

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instance = await db.Set<WorkflowInstance>().IgnoreQueryFilters().FirstAsync(i => i.Id == instanceId);

        Assert.NotNull(instance.PendingResumeAt);
        Assert.Equal(0, RecordingWorkflowBridge.ContinueCallCounts.GetValueOrDefault(instanceId));
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}

// Mimics only what this test needs from a real resume: acknowledging the call, then clearing the
// delay-resume state the same way the real WorkflowBridge.ContinueDelayedWorkflowAsync does at the
// end of a successful resume.
file sealed class RecordingWorkflowBridge(ApplicationDbContext dbContext) : IWorkflowBridge
{
    public static readonly ConcurrentDictionary<Guid, int> ContinueCallCounts = new();

    public async Task<bool> ContinueDelayedWorkflowAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        ContinueCallCounts.AddOrUpdate(workflowInstanceId, 1, (_, count) => count + 1);

        // IgnoreQueryFilters: called from WorkflowDelayResumeBackgroundService's own sweep scope,
        // which has no ambient authenticated HttpContext either.
        var instance = await dbContext.Set<WorkflowInstance>().IgnoreQueryFilters().FirstAsync(i => i.Id == workflowInstanceId, ct);
        instance.ClearPendingResume();
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct, Guid? existingInstanceId = null) =>
        throw new NotImplementedException();
    public Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct) =>
        throw new NotImplementedException();
    public Task<bool> RetryFailedStepAsync(Guid workflowInstanceId, CancellationToken ct) => throw new NotImplementedException();
}
