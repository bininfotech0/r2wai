using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Migration CaseInsensitiveApplicationNameAndCodeUniqueness adds unique lower(Name)/lower(Code)
/// indexes, but names were never unique before it, so a real database can already hold live
/// duplicates. A failed index build leaves this and every later migration unapplied while the API
/// starts anyway (Program.cs only logs the failure). The migration must rename duplicates first.
/// </summary>
[Trait("Category", "Integration")]
public class ApplicationUniquenessMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20261008195218_AddToolExecutions";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
                builder.UseSetting("Authentication:Jwt:SecretKey", "TestingSecretKeyForIntegrationTestsThatIsLongEnough!");
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
                        d.ServiceType == typeof(IHostedService) ||
                        d.ServiceType.FullName?.Contains("Npgsql") == true ||
                        d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    ).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseNpgsql(_postgres.GetConnectionString());
                        options.ConfigureWarnings(w => w.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });
                    services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
                });
            });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    // Rows are written with SQL against the schema as of MigrationBefore, not through the current EF
    // model: that model also carries every column added by later migrations (WorkspaceId, ...), and
    // its save pipeline reads tables those migrations create, so it cannot write to an older schema.
    private async Task SeedTenantAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Tenants" ("Id", "Name", "Slug", "Status", "CreatedAt", "IsDeleted")
            VALUES ({0}, 'Migration test', 'migration-test', 'Active', now(), false)
            ON CONFLICT ("Id") DO NOTHING
            """,
            TenantId);
    }

    private async Task<Guid> AddAsync(string name, string code, bool deleted = false)
    {
        // One statement per row, stamped with clock_timestamp(): the migration keeps the oldest of
        // a duplicate group by CreatedAt.
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Applications" ("Id", "TenantId", "DepartmentId", "Name", "Code", "CreatedAt", "IsDeleted")
            VALUES ({0}, {1}, NULL, {2}, {3}, clock_timestamp(), {4})
            """,
            id, TenantId, name, code, deleted);
        return id;
    }

    private async Task<(string Name, string Code)> LoadAsync(Guid id)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Set<ConnectedApplication>().IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == id);
        return (row.Name, row.Code);
    }

    [Fact]
    public async Task Up_RenamesLiveDuplicatesInsteadOfFailing_KeepsTheOldest_AndLeavesDeletedRowsAlone()
    {
        if (!_dockerAvailable) return;

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
        }
        await SeedTenantAsync();

        // Allowed before this migration: names were not unique, codes only case-sensitively.
        var original = await AddAsync("Zeta Portal", "ZT");
        var caseTwin = await AddAsync("zeta portal", "zt");
        var exactTwin = await AddAsync("Zeta Portal", "ZT-2");
        var deletedTwin = await AddAsync("Zeta Portal", "Zt", deleted: true);

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync(); // must not throw

            var indexes = await db.Database
                .SqlQueryRaw<string>("""SELECT indexname AS "Value" FROM pg_indexes WHERE tablename = 'Applications'""")
                .ToListAsync();
            Assert.Contains("IX_Applications_TenantId_LowerName", indexes);
            Assert.Contains("IX_Applications_TenantId_LowerCode_NoDepartment", indexes);
        }

        Assert.Equal(("Zeta Portal", "ZT"), await LoadAsync(original));
        Assert.Equal(($"zeta portal ({caseTwin.ToString()[..8]})", $"zt_{caseTwin.ToString()[..8]}"), await LoadAsync(caseTwin));
        Assert.Equal(($"Zeta Portal ({exactTwin.ToString()[..8]})", "ZT-2"), await LoadAsync(exactTwin)); // its code never clashed
        Assert.Equal(("Zeta Portal", "Zt"), await LoadAsync(deletedTwin));
    }
}
