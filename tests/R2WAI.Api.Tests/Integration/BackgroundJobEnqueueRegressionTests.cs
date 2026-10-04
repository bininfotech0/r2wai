using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Reproduces a real production incident: BackgroundJobQueue.EnqueueAsync originally reused the
/// ambient/request-scoped ApplicationDbContext (via IRepository&lt;BackgroundJob&gt;). Its only real
/// caller for documents, DocumentUploadedEventHandler, runs from inside
/// ApplicationDbContext.SaveChangesAsync's own DispatchDomainEventsAsync step -- BEFORE that call's
/// ClearDomainEvents() runs. Calling SaveChangesAsync again there recursively re-entered the same
/// override, re-scanned the still-dirty ChangeTracker, re-found the same not-yet-cleared
/// DocumentUploadedEvent, and re-published it -- confirmed live: one document upload produced 5000+
/// duplicate BackgroundJob rows for the same document before the request ever returned. Same failure
/// shape NotificationService.SendAsync was already fixed for elsewhere in this codebase; the fix here
/// is the identical isolated-scope pattern.
/// </summary>
[Trait("Category", "Integration")]
public class BackgroundJobEnqueueRegressionTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
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
                        // Deliberately keep BackgroundJobProcessor OUT of this run (no need for it to
                        // actually process the job -- this test only cares how many rows EnqueueAsync
                        // itself creates for one upload) by still removing IHostedService.
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

        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private async Task<HttpClient> GetAuthClientAsync()
    {
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString()!;

        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task UploadDocument_EnqueuesExactlyOneIndexDocumentJob()
    {
        if (!_dockerAvailable) return;
        var client = await GetAuthClientAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("regression test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "regression.txt");

        var uploadResponse = await client.PostAsync("/api/v1/documents/upload", content);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.IsSuccessStatusCode, $"Upload failed: {uploadResponse.StatusCode} — {uploadBody}");

        var documentId = JsonDocument.Parse(uploadBody).RootElement.GetProperty("id").GetGuid();

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // The bug produced 5000+ rows for a single upload before the request even returned -- an
        // upper bound well below that (10) still fails loudly on any reappearance of the loop
        // without being brittle about the exact real count (1, expected).
        var jobCount = await db.BackgroundJobs.CountAsync(j => j.PayloadJson.Contains(documentId.ToString()));
        Assert.True(jobCount < 10, $"Expected ~1 BackgroundJob for this upload, found {jobCount} -- recursive enqueue loop regression.");
        Assert.Equal(1, jobCount);
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
