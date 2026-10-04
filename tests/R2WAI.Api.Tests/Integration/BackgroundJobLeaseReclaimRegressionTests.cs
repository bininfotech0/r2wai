using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Implementation plan Phase 5's crash-simulation regression test: before this phase, a
/// BackgroundJob claimed into Processing (BackgroundJobProcessor's atomic UPDATE) whose replica
/// then crashed before ever reaching MarkSucceeded/MarkFailed stayed stuck in Processing forever —
/// the due-jobs sweep only ever looked at Status == Pending, so nothing would ever pick it back up.
/// This test reproduces exactly that: a job is put into Processing with an already-expired lease
/// (standing in for "a replica claimed it 10 minutes ago and never came back"), with no live
/// process actually holding it, then asserts the real BackgroundJobProcessor — running for real in
/// this test's own host, unlike BackgroundJobEnqueueRegressionTests which deliberately excludes it
/// — reclaims and processes it exactly once on its own next poll.
/// </summary>
[Trait("Category", "Integration")]
public class BackgroundJobLeaseReclaimRegressionTests : IAsyncLifetime
{
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
                    // Deliberately keep IHostedService registrations (unlike
                    // BackgroundJobEnqueueRegressionTests) — BackgroundJobProcessor actually running
                    // for real, on its own poll loop, is the entire point of this test.
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

                    services.AddScoped<IBackgroundJobHandler, RecordingJobHandler>();
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);

        // WebApplicationFactory only starts IHostedServices once something touches the server —
        // CreateClient() forces that without needing an actual HTTP call for this test.
        _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AbandonedLease_IsReclaimedAfterExpiry_AndProcessedExactlyOnce()
    {
        if (!_dockerAvailable) return;

        var payloadKey = Guid.NewGuid().ToString();
        Guid jobId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = new BackgroundJob(Guid.NewGuid(), RecordingJobHandler.JobTypeName, payloadKey);
            db.Set<BackgroundJob>().Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            // Stand-in for "a replica claimed this 10 minutes ago and then crashed": Processing,
            // with a lease that's already well past expiry. No process anywhere actually holds
            // this job — the real crash-duplication risk this phase closes.
            await db.Set<BackgroundJob>()
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, BackgroundJobStatus.Processing)
                    .SetProperty(j => j.LeaseOwner, "dead-replica")
                    .SetProperty(j => j.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-10)));
        }

        // BackgroundJobProcessor polls every 5s — poll for completion rather than a fixed sleep so
        // this isn't flaky on a slow box and doesn't wait longer than it has to on a fast one.
        BackgroundJob? finalState = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(500);
            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = await db.Set<BackgroundJob>().FirstAsync(j => j.Id == jobId);
            if (job.Status is BackgroundJobStatus.Succeeded or BackgroundJobStatus.DeadLettered or BackgroundJobStatus.Failed)
            {
                finalState = job;
                break;
            }
        }

        Assert.NotNull(finalState);
        Assert.Equal(BackgroundJobStatus.Succeeded, finalState!.Status);
        Assert.Equal(1, RecordingJobHandler.InvocationCounts.GetValueOrDefault(payloadKey));
        Assert.Null(finalState.LeaseOwner);
        Assert.Null(finalState.LeaseExpiresAt);
    }

    [Fact]
    public async Task ActivelyLeasedJob_BeforeExpiry_IsNotReclaimed()
    {
        if (!_dockerAvailable) return;

        // Negative control: a lease that hasn't expired yet must NOT be reclaimed — proves the fix
        // is a real expiry check, not an accidental "reclaim every Processing row" regression.
        var payloadKey = Guid.NewGuid().ToString();
        Guid jobId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = new BackgroundJob(Guid.NewGuid(), RecordingJobHandler.JobTypeName, payloadKey);
            db.Set<BackgroundJob>().Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            await db.Set<BackgroundJob>()
                .Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, BackgroundJobStatus.Processing)
                    .SetProperty(j => j.LeaseOwner, "live-replica")
                    .SetProperty(j => j.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(10)));
        }

        await Task.Delay(TimeSpan.FromSeconds(12)); // let at least two real poll cycles pass

        using var verifyScope = _factory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var finalState = await verifyDb.Set<BackgroundJob>().FirstAsync(j => j.Id == jobId);

        Assert.Equal(BackgroundJobStatus.Processing, finalState.Status);
        Assert.Equal("live-replica", finalState.LeaseOwner);
        Assert.Equal(0, RecordingJobHandler.InvocationCounts.GetValueOrDefault(payloadKey));
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}

file sealed class RecordingJobHandler : IBackgroundJobHandler
{
    public const string JobTypeName = "LeaseReclaimCrashSimTestJob";
    public static readonly ConcurrentDictionary<string, int> InvocationCounts = new();

    public string JobType => JobTypeName;

    public Task HandleAsync(string payloadJson, CancellationToken ct)
    {
        InvocationCounts.AddOrUpdate(payloadJson, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}
