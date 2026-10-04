using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Reproduces the ROADMAP.md 2026-08-20 addendum bug: a mid-AI-call audit write sharing the chat
/// request's DbContext could corrupt the change tracker's view of the conversation's first (user)
/// message, causing the final SaveChanges (persisting the assistant's reply) to throw
/// DbUpdateConcurrencyException. The reply still reached the caller — only the DB write silently failed.
///
/// <see cref="FakeToolDenialAiService"/> stands in for a real LLM tool-calling round trip: instead of
/// waiting on Ollama and hoping the model happens to invoke a governed tool, it directly performs the
/// same isolated-scope AuditLog write that AiFunctionAuditFilter.WriteAuditAsync does when a tool call
/// is denied mid-call. That's the actual mechanism under test, not the LLM's tool-choice behavior.
/// </summary>
[Trait("Category", "Integration")]
public class ChatConcurrencyRegressionTests : IAsyncLifetime
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SeededAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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

                    // Swap the real Semantic Kernel service for one that simulates a mid-call, isolated-
                    // scope audit write (what AiFunctionAuditFilter does on tool denial) without needing
                    // a live LLM to actually choose to call a governed tool.
                    services.RemoveAll<IAIService>();
                    services.AddScoped<IAIService, FakeToolDenialAiService>();
                });
            });

        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);

        // A tool the seeded admin cannot use without triggering AiFunctionAuditFilter's denial path —
        // ApprovalRequired is the simplest trigger (doesn't depend on the admin's role assignments).
        var deniedTool = new ToolDefinition(Guid.NewGuid(), SeededTenantId, FakeToolDenialAiService.DeniedToolName,
            ToolType.SemanticKernelFunction, "Test-only tool that always requires approval");
        deniedTool.ConfigureGovernance("Medium", requiredRole: null, confirmationRequired: false,
            approvalRequired: true, auditRequired: true);
        db.Set<ToolDefinition>().Add(deniedTool);
        await db.SaveChangesAsync();
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
    public async Task ChatWithAssistant_WhenAuditWriteHappensMidCall_PersistsBothMessages()
    {
        if (!_dockerAvailable) return;
        var client = await GetAuthClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Concurrency Regression Assistant",
            Type = "General",
            SystemPrompt = "You are a test assistant."
        });
        Assert.True(createResponse.IsSuccessStatusCode, $"Create assistant failed: {createResponse.StatusCode}");
        var createdBody = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var assistantId = createdBody.RootElement.GetProperty("id").GetGuid();

        var chatResponse = await client.PostAsJsonAsync($"/api/v1/assistants/{assistantId}/chat", new
        {
            AssistantId = assistantId,
            Message = "Trigger a mid-call audit write, then reply."
        });

        var chatBody = await chatResponse.Content.ReadAsStringAsync();
        Assert.True(chatResponse.StatusCode == HttpStatusCode.OK,
            $"Chat call failed: {chatResponse.StatusCode} — {chatBody}");

        var chatDoc = JsonDocument.Parse(chatBody);
        var conversationId = chatDoc.RootElement.GetProperty("conversationId").GetGuid();
        var reply = chatDoc.RootElement.GetProperty("reply").GetString();
        Assert.Equal(FakeToolDenialAiService.CannedReply, reply);

        // The bug: the HTTP response above can look perfectly fine (200, correct reply text) while the
        // assistant's message silently failed to reach the database. Only a direct DB read catches that.
        //
        // IgnoreQueryFilters: this raw scope has no ambient authenticated HttpContext — the chat call
        // above went through the real authenticated `client`, but that context doesn't carry over to a
        // separately-created scope. conversationId is already trustworthy (came from that authenticated
        // response), so this is a safe read-back, not a security-relevant lookup.
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.Conversations
            .IgnoreQueryFilters()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        Assert.NotNull(conversation);
        Assert.Equal(2, conversation!.Messages.Count);
        Assert.Contains(conversation.Messages, m => m.Role == MessageRole.User);
        Assert.Contains(conversation.Messages, m => m.Role == MessageRole.Assistant && m.Content == FakeToolDenialAiService.CannedReply);
    }
}

/// <summary>
/// Simulates a real chat call that invokes a governed tool the caller isn't allowed to use — but drives
/// the REAL, DI-resolved <see cref="AiFunctionAuditFilter"/> (same scope as the chat request, exactly
/// as the real Semantic Kernel pipeline would) through a trivial native function invocation, instead of
/// hand-rolling the audit write. That's what makes this test sensitive to whichever version of
/// AiFunctionAuditFilter is actually checked out — a hand-rolled "isolated scope" simulation would pass
/// unconditionally regardless of whether the real fix is present.
/// </summary>
public class FakeToolDenialAiService : IAIService
{
    public const string CannedReply = "This action requires administrator approval and cannot be performed automatically yet.";
    public const string DeniedToolName = "concurrency_test_denied_tool";

    private readonly AiFunctionAuditFilter _auditFilter;

    public FakeToolDenialAiService(AiFunctionAuditFilter auditFilter)
    {
        _auditFilter = auditFilter;
    }

    public async Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
    {
        // FunctionInvocationContext has no accessible public constructor, so the only way to exercise
        // the real filter is through the real SK pipeline: register it on a Kernel and invoke a trivial
        // native function through kernel.InvokeAsync — no LLM involved, the function body never runs
        // because EvaluateGovernance denies it (see the seeded ToolDefinition) before next() is called.
        var kernel = new Kernel();
        kernel.FunctionInvocationFilters.Add(_auditFilter);
        Func<string> unreachable = () =>
            throw new InvalidOperationException("Test setup bug: tool call was not denied — governance check didn't trigger.");
        var function = KernelFunctionFactory.CreateFromMethod(unreachable, DeniedToolName);

        await kernel.InvokeAsync(function, cancellationToken: ct);

        return CannedReply;
    }

    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => Task.FromResult(CannedReply);
    public IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
        => throw new NotImplementedException();
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
