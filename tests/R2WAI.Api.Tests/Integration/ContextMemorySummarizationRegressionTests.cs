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
/// "AI:ContextMemory:SummarizationEnabled" on (see ARCHITECTURE.md's Adoption Status). Drives
/// IChatService directly in-process rather than over HTTP: R2WAI.Api has no controller/hub route for
/// it today (only the Blazor Web project's ChatSessionService/CopilotPanel call it), so this is a
/// service-level integration test against the real DI graph — real EF/Postgres, only IAIService faked
/// to avoid needing a live LLM.
/// </summary>
[Trait("Category", "Integration")]
public class ContextMemorySummarizationRegressionTests : IAsyncLifetime
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SeededAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private PostgreSqlContainer? _postgres;
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
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private (WebApplicationFactory<Program> Factory, FakeSummarizingAiService FakeAi) CreateFactory(bool summarizationEnabled)
    {
        var fakeAi = new FakeSummarizingAiService();

        var factory = new WebApplicationFactory<Program>()
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
                    services.AddSingleton<IAIService>(fakeAi);
                });
            });

        return (factory, fakeAi);
    }

    private async Task EnsureSeededAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);
    }

    private async Task<Guid> CreateConversationWithPriorMessagesAsync(WebApplicationFactory<Program> factory, int priorMessageCount)
    {
        Guid conversationId;
        using (var scope = factory.Services.CreateScope())
        {
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
            var conversationDto = await chatService.CreateConversationAsync(
                SeededTenantId, SeededAdminId, "Context Memory Regression", "chat", null);
            conversationId = conversationDto.Id;
        }

        // Fresh scope/DbContext, matching how a real request would load the conversation,
        // instead of reusing the tracked instance from the scope that just created it.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conversation = await db.Conversations.FirstAsync(c => c.Id == conversationId);
            for (var i = 0; i < priorMessageCount; i++)
            {
                var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
                var message = conversation.AddMessage(Guid.NewGuid(), null, role, $"Prior turn #{i}");
                // conversation was loaded (tracked Unchanged), not Add()-ed: EF Core can't tell a
                // client-generated-Guid child discovered only via navigation fixup is new, so it
                // defaults to Modified. See the matching fix in ChatService/ChatWithAssistantCommand.
                db.Messages.Add(message);
            }
            await db.SaveChangesAsync();
        }

        return conversationId;
    }

    [Fact]
    public async Task SendMessage_SummarizationEnabled_WithMoreThanRecentWindow_CallsSummarizer()
    {
        if (!_dockerAvailable) return;
        var (factory, fakeAi) = CreateFactory(summarizationEnabled: true);
        await using var _ = factory;
        await EnsureSeededAsync(factory);

        // 12 prior messages + the one SendMessageAsync is about to add = 13 total, well past the
        // 10-turn recent window, so BuildConversationContextAsync must split and summarize.
        var conversationId = await CreateConversationWithPriorMessagesAsync(factory, priorMessageCount: 12);

        using var scope = factory.Services.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var reply = await chatService.SendMessageAsync(
            conversationId, SeededTenantId, SeededAdminId, "What's the latest status?", attachments: null);

        Assert.Equal(FakeSummarizingAiService.CannedReply, reply.Content);
        Assert.True(fakeAi.SummarizeCallCount > 0,
            "Expected ConversationMemoryService to call SummarizeTextAsync once the conversation exceeded the recent-turn window.");

        // The bug class this guards against (see ChatConcurrencyRegressionTests): a 200-looking reply
        // that silently failed to persist. Verify both messages actually reached the database.
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.Conversations.Include(c => c.Messages).FirstAsync(c => c.Id == conversationId);
        Assert.Equal(14, conversation.Messages.Count); // 12 seeded + 1 new user + 1 new assistant
        Assert.Contains(conversation.Messages, m => m.Role == MessageRole.Assistant && m.Content == FakeSummarizingAiService.CannedReply);
    }

    [Fact]
    public async Task SendMessage_SummarizationDisabled_WithMoreThanRecentWindow_NeverCallsSummarizer()
    {
        if (!_dockerAvailable) return;
        var (factory, fakeAi) = CreateFactory(summarizationEnabled: false);
        await using var _ = factory;
        await EnsureSeededAsync(factory);

        var conversationId = await CreateConversationWithPriorMessagesAsync(factory, priorMessageCount: 12);

        using var scope = factory.Services.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var reply = await chatService.SendMessageAsync(
            conversationId, SeededTenantId, SeededAdminId, "What's the latest status?", attachments: null);

        Assert.Equal(FakeSummarizingAiService.CannedReply, reply.Content);
        Assert.Equal(0, fakeAi.SummarizeCallCount);
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
        bool enableTools = false, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return CannedReply;
    }

    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _summarizeCallCount);
        return Task.FromResult(CannedSummary);
    }

    public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, CancellationToken ct = default) => Task.FromResult(CannedReply);
    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> ExtractDataAsync(string text, string schema, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
