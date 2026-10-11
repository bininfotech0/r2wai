using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Chatbot session memory over the real anonymous routes (chat, chat/stream, webhook) and the
/// retention sweep, on real Postgres: turn ordering depends on its microsecond timestamps, and the
/// sweep's ExecuteDeleteAsync is relational-only, so the InMemory provider would prove neither.
/// The fake AI records the context text each call received, which is what "the bot remembers"
/// means at this layer.
/// </summary>
[Trait("Category", "Integration")]
public class ChatbotSessionMemoryRegressionTests : IAsyncLifetime
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
                    services.AddSingleton<IEncryptionService, SessionMemoryPassThroughEncryption>();
                    services.RemoveAll<IAIService>();
                    services.AddScoped<IAIService, ContextRecordingAiService>();
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

    private async Task<Guid> SeedActiveChatbotAsync(string name, string? webhookKey = null)
    {
        var chatbotId = Guid.NewGuid();
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var chatbot = new Chatbot(chatbotId, SeededTenantId, SeededAdminId, name);
        chatbot.UpdateStatus(ChatbotStatus.Active);
        if (webhookKey is not null)
            chatbot.SetWebhookApiKey(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(webhookKey))), webhookKey[..6]);
        db.Chatbots.Add(chatbot);
        await db.SaveChangesAsync();
        return chatbotId;
    }

    private async Task SeedTurnsAsync(Guid chatbotId, string sessionId, DateTime createdAt, params (MessageRole Role, string Content)[] turns)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < turns.Length; i++)
            db.ChatbotSessionTurns.Add(new ChatbotSessionTurn(Guid.NewGuid(), SeededTenantId, chatbotId, sessionId,
                turns[i].Role, turns[i].Content, createdAt.AddMilliseconds(i)));
        await db.SaveChangesAsync();
    }

    private async Task<List<ChatbotSessionTurn>> LoadTurnsAsync(Guid chatbotId, string? sessionId = null)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ChatbotSessionTurns.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.ChatbotId == chatbotId && (sessionId == null || t.SessionId == sessionId))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    // The key a widget-route session is stored under.
    private static string Widget(string sessionId) => ChatbotSessionIds.Scope(ChatbotSessionIds.WidgetChannel, sessionId)!;

    private async Task<HttpResponseMessage> ChatAsync(Guid chatbotId, string message, string? sessionId, WebApplicationFactory<Program>? factory = null)
    {
        var response = await (factory ?? _factory!).CreateClient()
            .PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat", new { message, sessionId });
        return response;
    }

    private async Task ChatOkAsync(Guid chatbotId, string message, string? sessionId)
    {
        var response = await ChatAsync(chatbotId, message, sessionId);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Chat failed: {response.StatusCode} — {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Chat_SameSession_SecondMessageSeesTheFirstExchange_InOrder()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Memory Bot");
        var sessionId = Guid.NewGuid().ToString();

        await ChatOkAsync(chatbotId, "I am Zed from Pune.", sessionId);
        Assert.Null(ContextRecordingAiService.LastContextFor("What is my name?")); // nothing stored yet when asked below
        await ChatOkAsync(chatbotId, "What is my name?", sessionId);

        var context = ContextRecordingAiService.LastContextFor("What is my name?");
        Assert.NotNull(context);
        Assert.Contains("User: I am Zed from Pune.", context);
        Assert.Contains($"Assistant: {ContextRecordingAiService.ReplyTo("I am Zed from Pune.")}", context);
        Assert.True(context!.IndexOf("User: I am Zed", StringComparison.Ordinal)
            < context.IndexOf("Assistant:", StringComparison.Ordinal), "Visitor turn must precede the reply.");

        var turns = await LoadTurnsAsync(chatbotId, Widget(sessionId));
        Assert.Equal(
            [MessageRole.User, MessageRole.Assistant, MessageRole.User, MessageRole.Assistant],
            turns.Select(t => t.Role));
        // Distinct timestamps: Postgres keeps microseconds, so the pair's order must not tie.
        Assert.Equal(turns.Count, turns.Select(t => t.CreatedAt).Distinct().Count());
    }

    [Fact]
    public async Task Chat_OtherSessionOrOtherChatbot_NeverSeesThisSessionsTurns()
    {
        if (!_dockerAvailable) return;
        var chatbotA = await SeedActiveChatbotAsync("Isolation Bot A");
        var chatbotB = await SeedActiveChatbotAsync("Isolation Bot B");
        var sessionId = Guid.NewGuid().ToString();

        await ChatOkAsync(chatbotA, "Secret: ORCHID-7", sessionId);

        await ChatOkAsync(chatbotA, "Other visitor on bot A", Guid.NewGuid().ToString());
        Assert.Null(ContextRecordingAiService.LastContextFor("Other visitor on bot A"));

        // Same session id, different chatbot: ids are scoped per chatbot.
        await ChatOkAsync(chatbotB, "Same id on bot B", sessionId);
        Assert.Null(ContextRecordingAiService.LastContextFor("Same id on bot B"));
    }

    [Fact]
    public async Task Chat_WithoutSessionId_StoresNothing_AndSendsNoHistory()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Stateless Bot");

        await ChatOkAsync(chatbotId, "Stateless one", null);
        await ChatOkAsync(chatbotId, "Stateless two", null);

        Assert.Null(ContextRecordingAiService.LastContextFor("Stateless two"));
        Assert.Empty(await LoadTurnsAsync(chatbotId));
    }

    [Fact]
    public async Task Chat_InvalidSessionId_IsTreatedAsNoSession()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Bad Session Bot");

        await ChatOkAsync(chatbotId, "Bad id message", "has spaces; and quotes'");

        Assert.Empty(await LoadTurnsAsync(chatbotId));
    }

    [Fact]
    public async Task Chat_WhenTheModelTimesOut_RemembersNothing()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Timeout Memory Bot");
        var sessionId = Guid.NewGuid().ToString();

        ContextRecordingAiService.Delay = TimeSpan.FromSeconds(30);
        try
        {
            var response = await ChatAsync(chatbotId, "This reply never arrives", sessionId);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            ContextRecordingAiService.Delay = TimeSpan.Zero;
        }

        Assert.Empty(await LoadTurnsAsync(chatbotId, Widget(sessionId)));
    }

    [Fact]
    public async Task StreamChat_SameSession_StoresTheWholeStreamedReply_AndRecallsIt()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Stream Memory Bot");
        var sessionId = Guid.NewGuid().ToString();
        var client = _factory!.CreateClient();

        var first = await client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat/stream",
            new { message = "Stream: my code is KIWI-3", sessionId });
        Assert.Contains("event: done", await first.Content.ReadAsStringAsync());

        var turns = await LoadTurnsAsync(chatbotId, Widget(sessionId));
        Assert.Equal(2, turns.Count);
        // The fake streams its reply in several chunks; the stored turn is all of them joined.
        Assert.Equal(ContextRecordingAiService.ReplyTo("Stream: my code is KIWI-3"), turns[1].Content);

        var second = await client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat/stream",
            new { message = "Stream: what is my code?", sessionId });
        Assert.Contains("event: done", await second.Content.ReadAsStringAsync());
        Assert.Contains("User: Stream: my code is KIWI-3", ContextRecordingAiService.LastContextFor("Stream: what is my code?"));
    }

    [Fact]
    public async Task Webhook_CallerThreadKey_GivesMemoryAcrossMessages()
    {
        if (!_dockerAvailable) return;
        const string key = "whk_test_session_memory_key";
        var chatbotId = await SeedActiveChatbotAsync("Webhook Memory Bot", key);
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Webhook-Key", key);

        var first = await client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/webhook",
            new { message = "Webhook: I ordered a blue lamp", sessionId = "whatsapp:+919800000001" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/webhook",
            new { message = "Webhook: what did I order?", sessionId = "whatsapp:+919800000001" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Contains("User: Webhook: I ordered a blue lamp", ContextRecordingAiService.LastContextFor("Webhook: what did I order?"));
    }

    [Fact]
    public async Task AnonymousChat_WithAWebhookThreadKey_NeverSeesTheWebhookConversation()
    {
        if (!_dockerAvailable) return;
        const string key = "whk_test_channel_isolation_key";
        const string threadKey = "whatsapp:+919800000002";
        var chatbotId = await SeedActiveChatbotAsync("Channel Isolation Bot", key);
        var webhookClient = _factory!.CreateClient();
        webhookClient.DefaultRequestHeaders.Add("X-Webhook-Key", key);

        var stored = await webhookClient.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/webhook",
            new { message = "Webhook: my account PIN is 4417", sessionId = threadKey });
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);

        // The anonymous widget routes need no key; a guessed thread key must not open that history.
        await ChatOkAsync(chatbotId, "Probe: what did I tell you earlier?", threadKey);
        Assert.Null(ContextRecordingAiService.LastContextFor("Probe: what did I tell you earlier?"));

        var stream = await _factory.CreateClient().PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/chat/stream",
            new { message = "Stream probe: what did I tell you earlier?", sessionId = threadKey });
        Assert.Contains("event: done", await stream.Content.ReadAsStringAsync());
        // It does see the probe above (same widget session) — just never the webhook conversation.
        var streamContext = ContextRecordingAiService.LastContextFor("Stream probe: what did I tell you earlier?");
        Assert.Contains("User: Probe: what did I tell you earlier?", streamContext);
        Assert.DoesNotContain("4417", streamContext);
    }

    [Fact]
    public async Task History_ExcludesTurnsPastRetention_AndKeepsOnlyTheRecentWindow()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Window Bot");
        var sessionId = Guid.NewGuid().ToString();

        await SeedTurnsAsync(chatbotId, Widget(sessionId), DateTime.UtcNow.AddDays(-30),
            (MessageRole.User, "Ancient turn"), (MessageRole.Assistant, "Ancient reply"));
        var recentStart = DateTime.UtcNow.AddMinutes(-10);
        var recent = Enumerable.Range(0, 30)
            .Select(i => (i % 2 == 0 ? MessageRole.User : MessageRole.Assistant, $"Recent turn #{i:D2}"))
            .ToArray();
        await SeedTurnsAsync(chatbotId, Widget(sessionId), recentStart, recent);

        await ChatOkAsync(chatbotId, "Window question", sessionId);

        var context = ContextRecordingAiService.LastContextFor("Window question")!;
        Assert.DoesNotContain("Ancient", context);
        // Default window is the last 20 turns: #10..#29 in, #00..#09 out.
        Assert.Contains("Recent turn #29", context);
        Assert.Contains("Recent turn #10", context);
        Assert.DoesNotContain("Recent turn #09", context);
    }

    [Fact]
    public async Task MemoryDisabled_NeitherReadsNorStoresTurns()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Disabled Memory Bot");
        var sessionId = Guid.NewGuid().ToString();
        await SeedTurnsAsync(chatbotId, Widget(sessionId), DateTime.UtcNow.AddMinutes(-1),
            (MessageRole.User, "Earlier turn"), (MessageRole.Assistant, "Earlier reply"));

        await using var disabled = _factory!.WithWebHostBuilder(b => b.UseSetting("Chatbots:SessionMemory:Enabled", "false"));
        var response = await ChatAsync(chatbotId, "Disabled question", sessionId, disabled);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Null(ContextRecordingAiService.LastContextFor("Disabled question"));
        Assert.Equal(2, (await LoadTurnsAsync(chatbotId, Widget(sessionId))).Count);
    }

    [Fact]
    public async Task RetentionSweep_HardDeletesExpiredTurns_AndHonoursAStricterTenantPolicy()
    {
        if (!_dockerAvailable) return;
        var chatbotId = await SeedActiveChatbotAsync("Retention Bot");
        await SeedTurnsAsync(chatbotId, "expired", DateTime.UtcNow.AddDays(-8), (MessageRole.User, "8 days old"));
        await SeedTurnsAsync(chatbotId, "policy", DateTime.UtcNow.AddDays(-3), (MessageRole.User, "3 days old"));
        await SeedTurnsAsync(chatbotId, "fresh", DateTime.UtcNow.AddHours(-1), (MessageRole.User, "1 hour old"));

        using (var scope = _factory!.Services.CreateScope())
        {
            var sweep = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
            var result = await sweep.RunSweepAsync();
            Assert.True(result.ChatbotSessionTurnsPurged >= 1);
        }
        // Default 7-day lifetime: only the 8-day-old row is gone.
        Assert.Equal(["policy", "fresh"], (await LoadTurnsAsync(chatbotId)).Select(t => t.SessionId));

        var policyId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GlobalPolicies.Add(new GlobalPolicy(policyId, SeededTenantId, "DataRetention",
                "Session memory test policy", "{\"retentionDays\":2}", isActive: true));
            await db.SaveChangesAsync();
        }
        try
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDataRetentionService>().RunSweepAsync();
            // The tenant's 2-day policy is stricter than the 7-day default, so the 3-day-old row goes too.
            Assert.Equal(["fresh"], (await LoadTurnsAsync(chatbotId)).Select(t => t.SessionId));
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GlobalPolicies.IgnoreQueryFilters().Where(p => p.Id == policyId).ExecuteDeleteAsync();
        }
    }
}

/// <summary>
/// Records the context text (session history plus knowledge) each chat call received, keyed by the
/// visitor message, since the AI service is scoped and each request gets a new instance.
/// </summary>
file sealed class ContextRecordingAiService : IAIService
{
    private static readonly ConcurrentDictionary<string, string?> ContextByMessage = new();

    /// <summary>Simulates a slow model; the tests in this class run sequentially.</summary>
    public static TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public static string ReplyTo(string message) => $"Reply to [{message}] part one, part two.";

    public static string? LastContextFor(string message) => ContextByMessage.GetValueOrDefault(message);

    public async Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
    {
        ContextByMessage[message] = conversationHistory;
        await Task.Delay(Delay, ct);
        return ReplyTo(message);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ContextByMessage[message] = conversationHistory;
        await Task.Delay(Delay, ct);
        var reply = ReplyTo(message);
        var split = reply.IndexOf(" part two", StringComparison.Ordinal);
        yield return reply[..split];
        yield return reply[split..];
    }

    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult("summary");
    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => Task.FromResult("ok");
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult("ok");
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult("ok");
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult("ok");
}

file sealed class SessionMemoryPassThroughEncryption : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
