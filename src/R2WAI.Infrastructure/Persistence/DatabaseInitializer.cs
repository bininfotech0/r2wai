using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace R2WAI.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            logger.LogInformation("Initializing database...");

            // Retry the connection instead of falling back to EnsureCreatedAsync: EnsureCreated
            // stamps the schema from the current model with no __EFMigrationsHistory row, which
            // permanently diverges the database from the normal MigrateAsync path below (every
            // future deploy would then fail to apply migrations against it). A container-startup
            // race with Postgres should just be waited out, not "solved" by skipping migrations.
            // 20 attempts x 5s = ~100s budget. Verified live against docker-compose: even with
            // Postgres already reported "healthy" by Compose's own healthcheck (depends_on:
            // condition: service_healthy), CanConnectAsync from a sibling container kept failing
            // for ~27s past that point on a cold start -- the earlier 10x3s (~27s) budget was
            // observed exhausting exactly at that boundary in practice, not just in theory.
            const int maxConnectAttempts = 20;
            var connected = false;
            Exception? lastConnectEx = null;
            for (var attempt = 1; attempt <= maxConnectAttempts; attempt++)
            {
                try
                {
                    connected = await context.Database.CanConnectAsync(cancellationToken);
                    lastConnectEx = null;
                    if (connected) break;
                }
                catch (Exception connectEx)
                {
                    lastConnectEx = connectEx;
                }

                if (attempt < maxConnectAttempts)
                {
                    logger.LogWarning("Database not reachable yet (attempt {Attempt}/{MaxAttempts}): {Message}. Retrying in 5s...",
                        attempt, maxConnectAttempts, lastConnectEx?.Message ?? "CanConnectAsync returned false");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            if (!connected)
            {
                logger.LogError(lastConnectEx, "Could not connect to the database after {MaxAttempts} attempts. Ensure PostgreSQL is running and accessible; migrations were not applied.", maxConnectAttempts);
                return;
            }

            var availableMigrations = context.Database.GetMigrations().ToList();
            if (availableMigrations.Count == 0)
            {
                logger.LogInformation("No migrations found. Applying schema via Migrate...");
                await context.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

                if (appliedMigrations.Count == 0 && await HasExistingApplicationSchemaAsync(context, cancellationToken))
                {
                    await BaselineInitialMigrationAsync(context, logger, availableMigrations, cancellationToken);
                }

                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count > 0)
                {
                    logger.LogInformation("Applying {MigrationCount} pending database migrations...", pendingMigrations.Count);
                    await context.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    logger.LogInformation("Database schema is up to date.");
                }
            }

            logger.LogInformation("Seeding database...");
            await ApplicationDbContextSeed.SeedAsync(context, cancellationToken);

            logger.LogInformation("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    private static async Task<bool> HasExistingApplicationSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND (table_name = 'Tenants' OR table_name = 'tenants')
                );
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool boolResult && boolResult;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task BaselineInitialMigrationAsync(
        ApplicationDbContext context,
        ILogger logger,
        IReadOnlyList<string> availableMigrations,
        CancellationToken cancellationToken)
    {
        if (availableMigrations.Count != 1)
        {
            throw new InvalidOperationException(
                "The database already contains application tables but has no EF migration history. " +
                "Automatic recovery only supports a single initial migration. Please baseline the database manually.");
        }

        var migrationId = availableMigrations[0];
        var productVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? "10.0.0";

        logger.LogWarning(
            "Existing application tables were detected without EF migration history. Recording migration {MigrationId} as the baseline.",
            migrationId);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ({migrationId}, {productVersion})
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            cancellationToken);
    }
}
