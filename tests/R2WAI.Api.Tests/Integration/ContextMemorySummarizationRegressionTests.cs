using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Live-DB regression coverage for Phase 6's Context &amp; Memory summarization path
/// (ConversationMemoryService), which shipped off by default with only pure-logic unit tests
/// (ConversationContextBuilderTests) — this was the documented prerequisite before ever flipping
/// "AI:ContextMemory:SummarizationEnabled" on (see ARCHITECTURE.md's Adoption Status). Drives the
/// real HTTP routes (POST /api/v1/chat/conversations, POST .../messages → SendMessageCommand) the
/// same way ChatConcurrencyRegressionTests does, rather than a service directly — this previously
/// drove the now-deleted IChatService in-process, which was dead code with no live HTTP route.
/// SendMessageCommandHandler is the one real caller of IConversationMemoryService today.
/// </summary>
[Trait("Category", "Integration")]
public class ContextMemorySummarizationRegressionTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private FakeSummarizingAiService? _fakeAi;
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
        }
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(bool summarizationEnabled)
    {
        _fakeAi = new FakeSummarizingAiService();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Authentication:Jwt:SecretKey", "TestingSecretKeyForIntegrationTestsThatIsLongEnough!");
                builder.UseSetting("ConnectionStrings:Redis", "");
                builder.UseSetting("Cache:Redis:ConnectionString", "");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres!.GetConnectionString());
                builder.UseSetting("AI:ContextMemory:SummarizationEnabled", summarizationEnabled ? "true" : "false");

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

                    // Singleton, not scoped: the test asserts against this exact instance's call count
                    // after the action runs, so it must be the same object the app resolved.
                    services.RemoveAll<IAIService>();
                    services.AddSingleton<IAIService>(_fakeAi);
                });
            });

        _client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            await ApplicationDbContextSeed.SeedAsync(db);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });
        var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var token = loginBody.RootElement.GetProperty("token").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateConversationWithPriorMessagesAsync(HttpClient client, int priorMessageCount)
    {
        var createResponse = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Context Memory Regression",
            Module = "chat"
        });
        Assert.True(createResponse.IsSuccessStatusCode, $"Create conversation failed: {createResponse.StatusCode}");
        var createdBody = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var conversationId = createdBody.RootElement.GetProperty("id").GetGuid();

        // IgnoreQueryFilters: this raw scope has no ambient authenticated HttpContext (the conversation
        // was created via the real authenticated `client` above, but that context doesn't carry over
        // to a separately-created scope) — P0-5's fail-closed tenant filter would otherwise find
        // nothing here. conversationId itself is already trustworthy (came from the authenticated
        // create call), so this is a safe read-back, not a security-relevant lookup.
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.Conversations.IgnoreQueryFilters().FirstAsync(c => c.Id == conversationId);
        for (var i = 0; i < priorMessageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var message = conversation.AddMessage(Guid.NewGuid(), null, role, $"Prior turn #{i}");
            // conversation was loaded (tracked Unchanged), not Add()-ed: EF Core can't tell a
            // client-generated-Guid child discovered only via navigation fixup is new, so it
            // defaults to Modified. See the matching fix in SendMessageCommandHandler.
            db.Messages.Add(message);
        }
        await db.SaveChangesAsync();

        return conversationId;
    }

    private static async Task<string> SendMessageAsync(HttpClient client, Guid conversationId, string content)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(content), "content" }
        };
        var response = await client.PostAsync($"/api/v1/chat/conversations/{conversationId}/messages", form);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Send message failed: {response.StatusCode} — {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("content").GetString()!;
    }

    [Fact]
    public async Task SendMessage_SummarizationEnabled_WithMoreThanRecentWindow_CallsSummarizer()
    {
        if (!_dockerAvailable) return;
        var client = await CreateAuthenticatedClientAsync(summarizationEnabled: true);

        // 12 prior messages + the one being sent = 13 total, well past the 10-turn recent window,
        // so BuildConversationContextAsync must split and summarize.
        var conversationId = await CreateConversationWithPriorMessagesAsync(client, priorMessageCount: 12);

        var replyContent = await SendMessageAsync(client, conversationId, "What's the latest status?");

        Assert.Equal(FakeSummarizingAiService.CannedReply, replyContent);
        Assert.True(_fakeAi!.SummarizeCallCount > 0,
            "Expected ConversationMemoryService to call SummarizeTextAsync once the conversation exceeded the recent-turn window.");

        // The bug class this guards against (see ChatConcurrencyRegressionTests): a 200-looking reply
        // that silently failed to persist. Verify both messages actually reached the database.
        // IgnoreQueryFilters: no ambient authenticated HttpContext in this raw scope — see the earlier
        // CreateConversationWithPriorMessagesAsync note.
        using var verifyScope = _factory!.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.Conversations.IgnoreQueryFilters().Include(c => c.Messages).FirstAsync(c => c.Id == conversationId);
        Assert.Equal(14, conversation.Messages.Count); // 12 seeded + 1 new user + 1 new assistant
        Assert.Contains(conversation.Messages, m => m.Role == MessageRole.Assistant && m.Content == FakeSummarizingAiService.CannedReply);
    }

    [Fact]
    public async Task SendMessage_SummarizationDisabled_WithMoreThanRecentWindow_NeverCallsSummarizer()
    {
        if (!_dockerAvailable) return;
        var client = await CreateAuthenticatedClientAsync(summarizationEnabled: false);

        var conversationId = await CreateConversationWithPriorMessagesAsync(client, priorMessageCount: 12);

        var replyContent = await SendMessageAsync(client, conversationId, "What's the latest status?");

        Assert.Equal(FakeSummarizingAiService.CannedReply, replyContent);
        Assert.Equal(0, _fakeAi!.SummarizeCallCount);
    }
}

/// <summary>
/// Tracks SummarizeTextAsync invocations so the test can assert the summarization branch actually
/// ran (or didn't), without needing a live LLM to produce a real summary.
/// </summary>
public class FakeSummarizingAiService : IAIService
{
    public const string CannedReply = "Canned assistant reply for context-memory regression test.";
    public const string CannedSummary = "Canned summary of older turns.";

    private int _summarizeCallCount;
    public int SummarizeCallCount => _summarizeCallCount;

    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return CannedReply;
    }

    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _summarizeCallCount);
        return Task.FromResult(CannedSummary);
    }

    public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) => Task.FromResult(CannedReply);
    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
