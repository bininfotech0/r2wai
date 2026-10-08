using System.Net;
using System.Net.Http.Json;
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
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// The dashboard's per-agent ranking reads AssistantDefinition.UsageCount, but the public website
/// widget — the one publish channel with a working runtime — only ever bumped the chatbot's own
/// TotalMessagesServed. The credit is an atomic relational UPDATE that the InMemory test provider
/// skips, so only a real Postgres run proves it.
/// </summary>
[Trait("Category", "Integration")]
public class WidgetUsageCreditRegressionTests : IAsyncLifetime
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
                builder.UseSetting("Chatbots:ReplyTimeoutSeconds", "1");

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
                    services.AddSingleton<IEncryptionService, PassThroughEncryptionService>();
                    services.RemoveAll<IAIService>();
                    services.AddScoped<IAIService, CannedReplyAiService>();
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
    public async Task WidgetChat_CreditsTheLinkedAssistant_AndTheChatbot()
    {
        if (!_dockerAvailable) return;

        var assistantId = Guid.NewGuid();
        var chatbotId = Guid.NewGuid();
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<AssistantDefinition>().Add(new AssistantDefinition(assistantId, SeededTenantId, "Widget Credit Assistant", AssistantType.General));
            var chatbot = new Chatbot(chatbotId, SeededTenantId, SeededAdminId, "Widget Credit Bot");
            chatbot.AssignAssistant(assistantId);
            chatbot.UpdateStatus(ChatbotStatus.Active);
            db.Chatbots.Add(chatbot);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat", new { Message = "hello" });
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Widget chat failed: {response.StatusCode} — {await response.Content.ReadAsStringAsync()}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assistant = await db.Set<AssistantDefinition>().IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == assistantId);
            var bot = await db.Chatbots.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == chatbotId);

            Assert.Equal(1, assistant.UsageCount);
            Assert.Equal(1, bot.TotalMessagesServed);
        }
    }

    [Fact]
    public async Task WidgetChat_FillsPromptPlaceholders_BeforeTheModelSeesThem()
    {
        if (!_dockerAvailable) return;

        var chatbotId = Guid.NewGuid();
        string tenantName;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            tenantName = (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == SeededTenantId)).Name;
            var chatbot = new Chatbot(chatbotId, SeededTenantId, SeededAdminId, "Placeholder Bot");
            chatbot.UpdateDetails("Placeholder Bot", null, null, null,
                "Org {{tenant.name}}, workspace {{ workspace.name }}, user {{user.name}} ({{user.role}}), date {{current_date}}, keep {{ticket.id}}.");
            chatbot.UpdateStatus(ChatbotStatus.Active);
            db.Chatbots.Add(chatbot);
            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat", new { Message = "hello" });
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Widget chat failed: {response.StatusCode} — {await response.Content.ReadAsStringAsync()}");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.Equal(
            $"Org {tenantName}, workspace Placeholder Bot, user Guest (Guest), date {today}, keep {{{{ticket.id}}}}.",
            CannedReplyAiService.LastSystemPrompt);
    }
    private async Task<Guid> SeedActiveChatbotAsync(string name)
    {
        var chatbotId = Guid.NewGuid();
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var chatbot = new Chatbot(chatbotId, SeededTenantId, SeededAdminId, name);
        chatbot.UpdateStatus(ChatbotStatus.Active);
        db.Chatbots.Add(chatbot);
        await db.SaveChangesAsync();
        return chatbotId;
    }

    [Fact]
    public async Task WidgetChat_WhenTheModelIsTooSlow_Returns503QuicklyInsteadOfHanging()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Slow Model Bot");
        CannedReplyAiService.Delay = TimeSpan.FromSeconds(30);
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _factory!.CreateClient().PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat", new { Message = "hello" });
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("taking too long", await response.Content.ReadAsStringAsync());
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Took {stopwatch.Elapsed} — the reply timeout did not cut in.");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bot = await db.Chatbots.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == chatbotId);
            Assert.Equal(0, bot.TotalMessagesServed); // nothing was served, so nothing is counted
        }
        finally
        {
            CannedReplyAiService.Delay = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task WidgetStreamChat_WhenNoTextArrivesInTime_SendsARetryableErrorEvent()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Slow Stream Bot");
        CannedReplyAiService.Delay = TimeSpan.FromSeconds(30);
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _factory!.CreateClient().PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat/stream", new { Message = "hello" });
            var body = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            Assert.Contains("event: error", body);
            Assert.Contains("taking too long", body);
            Assert.DoesNotContain("event: chunk", body);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Took {stopwatch.Elapsed} — the first-text deadline did not cut in.");
        }
        finally
        {
            CannedReplyAiService.Delay = TimeSpan.Zero;
        }
    }
}

file sealed class CannedReplyAiService : IAIService
{
    private const string Reply = "Hello from the widget.";

    /// <summary>The system prompt of the most recent chat call, so a test can see what the model received.</summary>
    public static string? LastSystemPrompt { get; private set; }

    /// <summary>Simulates a slow local model; the tests in this class run sequentially.</summary>
    public static TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public async Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
        await Task.Delay(Delay, ct);
        return Reply;
    }
    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => Task.FromResult(Reply);
    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(Reply);
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(Reply);
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(Reply);
    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
        await Task.Delay(Delay, ct);
        yield return Reply;
    }
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(Reply);
}

file sealed class PassThroughEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
