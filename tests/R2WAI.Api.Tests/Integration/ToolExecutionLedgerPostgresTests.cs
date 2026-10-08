using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// ToolGovernanceFilterTests prove the ledger's logic on the InMemory provider, which enforces no
/// foreign keys. ToolExecutions has four (tenant, user, tool, approval), so this runs the real
/// gateway against real Postgres with every migration applied, signed in as the seeded admin.
/// </summary>
[Trait("Category", "Integration")]
public class ToolExecutionLedgerPostgresTests : IAsyncLifetime
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
                    services.AddSingleton<IEncryptionService, LedgerPassThroughEncryptionService>();
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

    private IServiceScope SignedInAsAdmin()
    {
        var scope = _factory!.Services.CreateScope();
        var claims = new List<Claim>
        {
            new("tenant_id", SeededTenantId.ToString()),
            new(ClaimTypes.NameIdentifier, SeededAdminId.ToString()),
            new(ClaimTypes.Role, "Admin"),
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    private static async Task InvokeAsync(IServiceScope scope, string plugin, string functionName, Guid? toolDefinitionId)
    {
        var function = KernelFunctionFactory.CreateFromMethod(
            () => "tool-ran",
            new KernelFunctionFromMethodOptions
            {
                FunctionName = functionName,
                AdditionalMetadata = toolDefinitionId is null
                    ? null
                    : new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                        new Dictionary<string, object?> { [AiFunctionAuditFilter.ToolDefinitionIdMetadataKey] = toolDefinitionId }),
            });
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromFunctions(plugin, [function]);
        kernel.FunctionInvocationFilters.Add(scope.ServiceProvider.GetRequiredService<AiFunctionAuditFilter>());
        await kernel.InvokeAsync(kernel.Plugins.GetFunction(plugin, functionName));
    }

    [Fact]
    public async Task Ledger_rows_satisfy_every_foreign_key_on_real_Postgres()
    {
        if (!_dockerAvailable) return;

        using var scope = SignedInAsAdmin();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lowRisk = new ToolDefinition(Guid.NewGuid(), SeededTenantId, "Ledger Low", ToolType.Http, "test", "https://api.example.test");
        lowRisk.ConfigureGovernance("Low", requiredRole: null, confirmationRequired: false, approvalRequired: false, auditRequired: true);
        var needsApproval = new ToolDefinition(Guid.NewGuid(), SeededTenantId, "Ledger Approval", ToolType.Http, "test", "https://api.example.test");
        needsApproval.ConfigureGovernance("High", requiredRole: null, confirmationRequired: false, approvalRequired: true, auditRequired: true);
        db.ToolDefinitions.AddRange(lowRisk, needsApproval);
        // The seed registers start_workflow as a real row; drop it in this throwaway database so the
        // call falls back to the code-defined default, whose fixed id must NOT be linked (it is no row).
        db.ToolDefinitions.RemoveRange(db.ToolDefinitions.Where(t => t.TenantId == SeededTenantId && t.Name == "start_workflow"));
        await db.SaveChangesAsync();

        await InvokeAsync(scope, "DynamicTools", "Ledger_Low", lowRisk.Id);              // registered tool → Succeeded
        await InvokeAsync(scope, "DynamicTools", "Ledger_Approval", needsApproval.Id);   // → AwaitingApproval + approval FK
        await InvokeAsync(scope, "WorkflowPlugin", "start_workflow", null);              // built-in default → no tool FK
        await InvokeAsync(scope, "Rogue", "delete_everything", null);                    // → Denied

        var rows = await db.ToolExecutions.AsNoTracking()
            .Where(e => e.TenantId == SeededTenantId && e.UserId == SeededAdminId)
            .ToListAsync();

        Assert.Contains(rows, e => e.ToolDefinitionId == lowRisk.Id && e.Status == ToolExecutionStatus.Succeeded);
        Assert.Contains(rows, e => e.ToolDefinitionId == needsApproval.Id && e.Status == ToolExecutionStatus.AwaitingApproval && e.ApprovalRequestId != null);
        Assert.Contains(rows, e => e.Function == "start_workflow" && e.ToolDefinitionId == null && e.Status == ToolExecutionStatus.Succeeded);
        Assert.Contains(rows, e => e.Function == "delete_everything" && e.Status == ToolExecutionStatus.Denied);
    }
}

file sealed class LedgerPassThroughEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
