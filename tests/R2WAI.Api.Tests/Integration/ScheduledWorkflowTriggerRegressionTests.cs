using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Api.Services;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// ScheduledWorkflowTriggerService (2026-10-16) against a real Postgres: it relies on a session
/// advisory lock and a jsonb containment check (EF.Functions.JsonContains) to start each schedule
/// occurrence exactly once across ticks and replicas — neither exists on the InMemory provider.
/// Also proves the start path works with no signed-in user: before the same-day WorkflowBridge fix,
/// every run started outside a request (webhook, schedule) 404'd on the fail-closed tenant filter.
/// </summary>
[Trait("Category", "Integration")]
public class ScheduledWorkflowTriggerRegressionTests : IAsyncLifetime
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

    private ScheduledWorkflowTriggerService NewSweeper() =>
        new(_factory!.Services, NullLogger<ScheduledWorkflowTriggerService>.Instance);

    private async Task<(Guid Id, DateTime Since)> AddWorkflowAsync(string trigger, bool publish)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workflow = new Workflow(Guid.NewGuid(), SeededTenantId, SeededAdminId, "Scheduled " + Guid.NewGuid(),
            null, null, "[]", trigger);
        if (publish) workflow.Publish();
        db.Workflows.Add(workflow);
        await db.SaveChangesAsync();
        // Read back what the sweeper will see, not the in-memory value SaveChanges may have re-stamped.
        var saved = await db.Workflows.IgnoreQueryFilters().AsNoTracking().SingleAsync(w => w.Id == workflow.Id);
        return (saved.Id, saved.ModifiedAt ?? saved.CreatedAt);
    }

    private async Task<List<WorkflowInstance>> InstancesAsync(Guid workflowId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.WorkflowInstances.IgnoreQueryFilters().Where(i => i.WorkflowId == workflowId).ToListAsync();
    }

    [Fact]
    public async Task PublishedSchedule_StartsEachOccurrenceExactlyOnce_AcrossTicksAndRacingReplicas()
    {
        if (!_dockerAvailable) return;

        var (id, since) = await AddWorkflowAsync("schedule:* * * * *|UTC", publish: true);
        // Two minute boundaries fall in (since, since + 2m 1s].
        var now = since.AddMinutes(2).AddSeconds(1);

        Assert.Equal(2, await NewSweeper().RunDueSchedulesAsync(now, CancellationToken.None));
        Assert.Equal(0, await NewSweeper().RunDueSchedulesAsync(now, CancellationToken.None));

        // Two "replicas" ticking at the same instant one minute later: the advisory lock plus the
        // scheduledFor marker must still yield exactly one new run between them.
        var later = now.AddMinutes(1);
        var raced = await Task.WhenAll(
            NewSweeper().RunDueSchedulesAsync(later, CancellationToken.None),
            NewSweeper().RunDueSchedulesAsync(later, CancellationToken.None));
        Assert.Equal(1, raced.Sum());

        var instances = await InstancesAsync(id);
        Assert.Equal(3, instances.Count);
        var data = instances.Select(i => JsonDocument.Parse(i.Data!).RootElement).ToList();
        Assert.All(data, d => Assert.Equal("schedule", d.GetProperty("trigger").GetString()));
        Assert.Equal(3, data.Select(d => d.GetProperty("scheduledFor").GetString()).Distinct().Count());
    }

    [Theory]
    [InlineData("schedule:* * * * *|UTC", false)] // draft: schedules only run once published
    [InlineData("Schedule", true)]               // the old wizard's bare label: no cron, nothing to fire
    [InlineData("Manual", true)]
    public async Task NonRunnableSchedules_NeverStart(string trigger, bool publish)
    {
        if (!_dockerAvailable) return;

        var (id, since) = await AddWorkflowAsync(trigger, publish);

        await NewSweeper().RunDueSchedulesAsync(since.AddMinutes(3), CancellationToken.None);

        Assert.Empty(await InstancesAsync(id));
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
