using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services.BackgroundJobs;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// User memory on real Postgres, through the real DI graph: the tenant filter, the two filtered
/// unique key indexes, ExecuteUpdate/ExecuteDelete and the audit redaction are all relational
/// behaviour the InMemory provider would not prove. The signed-in user is a settable fake so one
/// test can act as two people; the learning pass runs against a fake model with a scripted reply.
/// </summary>
[Trait("Category", "Integration")]
public class UserMemoryPostgresTests : IAsyncLifetime
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Admin = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StandardUser = Guid.Parse("00000000-0000-0000-0000-000000000002");

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

                    services.RemoveAll<ICurrentUserService>();
                    services.AddScoped<FakeCurrentUser>();
                    services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<FakeCurrentUser>());
                    services.RemoveAll<IAIService>();
                    services.AddScoped<IAIService, ScriptedLearningAiService>();
                    services.RemoveAll<IEncryptionService>();
                    services.AddSingleton<IEncryptionService, MemoryTestPassThroughEncryption>();
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

    /// <summary>A request scope signed in as <paramref name="userId"/> (null: anonymous).</summary>
    private AsyncServiceScope As(Guid? userId, bool apiKey = false)
    {
        var scope = _factory!.Services.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<FakeCurrentUser>();
        user.UserId = userId;
        user.TenantId = userId is null ? null : Tenant;

        var claims = new List<Claim>();
        if (apiKey) claims.Add(new Claim("auth_method", "api_key"));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        return scope;
    }

    private static IUserMemoryService Memory(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUserMemoryService>();

    private async Task<Guid> SeedAgentAsync(string name)
    {
        await using var scope = As(Admin);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var agent = new AssistantDefinition(Guid.NewGuid(), Tenant, name, AssistantType.General, null, null);
        db.AssistantDefinitions.Add(agent);
        await db.SaveChangesAsync();
        return agent.Id;
    }

    private static readonly AssistantBehaviorSettings ReadWrite = new() { UserMemory = AssistantBehaviorSettings.UserMemoryReadWrite };

    private static SaveUserMemoryRequest Save(string content, string? key = null, UserMemoryCategory category = UserMemoryCategory.Preference) =>
        new(category, key, content);

    [Fact]
    public async Task EachUser_SeesAndChangesOnlyTheirOwnMemories()
    {
        if (!_dockerAvailable) return;

        UserMemoryDto mine;
        await using (var scope = As(Admin))
            mine = await Memory(scope).CreateAsync(Save("Admin prefers tables.", "format_iso"));

        await using (var scope = As(StandardUser))
        {
            var overview = await Memory(scope).GetOverviewAsync();
            Assert.DoesNotContain(overview.Items, m => m.Id == mine.Id);
            await Assert.ThrowsAsync<R2WAI.Application.Common.Exceptions.NotFoundException>(() => Memory(scope).UpdateAsync(mine.Id, Save("hijack")));
            await Assert.ThrowsAsync<R2WAI.Application.Common.Exceptions.NotFoundException>(() => Memory(scope).DeleteAsync(mine.Id));
            Assert.Equal(0, await Memory(scope).ClearAsync("all"));

            // Keys are unique per user, not globally.
            await Memory(scope).CreateAsync(Save("User prefers prose.", "format_iso"));
        }

        await using (var scope = As(Admin))
        {
            var overview = await Memory(scope).GetOverviewAsync();
            Assert.Contains(overview.Items, m => m.Id == mine.Id && m.Content == "Admin prefers tables.");
            await Assert.ThrowsAsync<R2WAI.Application.Common.Exceptions.ValidationException>(() => Memory(scope).CreateAsync(Save("Again.", "Format ISO")));
        }
    }

    [Fact]
    public async Task PrepareTurn_GivesActivePersonalAndOwnNotesOnly_AndMarksThemUsed()
    {
        if (!_dockerAvailable) return;
        var agent = await SeedAgentAsync("Memory Agent A");
        var otherAgent = await SeedAgentAsync("Memory Agent B");

        await using (var scope = As(Admin))
        {
            var memory = Memory(scope);
            await memory.CreateAsync(Save("PT-ACTIVE likes brevity."));
            var paused = await memory.CreateAsync(Save("PT-PAUSED secret project."));
            await memory.UpdateAsync(paused.Id, Save("PT-PAUSED secret project.") with { Paused = true });

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserMemories.Add(new UserMemory(Guid.NewGuid(), Tenant, Admin, null, UserMemoryCategory.Fact, null,
                "PT-PENDING unreviewed.", UserMemorySource.Learned, UserMemoryStatus.PendingReview));
            db.UserMemories.Add(new UserMemory(Guid.NewGuid(), Tenant, Admin, agent, UserMemoryCategory.Fact, "a_note",
                "PT-OWN-NOTE from agent A.", UserMemorySource.Agent, UserMemoryStatus.Active));
            db.UserMemories.Add(new UserMemory(Guid.NewGuid(), Tenant, Admin, otherAgent, UserMemoryCategory.Fact, "b_note",
                "PT-OTHER-NOTE from agent B.", UserMemorySource.Agent, UserMemoryStatus.Active));
            await db.SaveChangesAsync();
        }

        await using (var scope = As(Admin))
        {
            var block = await Memory(scope).PrepareTurnAsync(agent, null);

            Assert.NotNull(block);
            Assert.Contains("PT-ACTIVE", block);
            Assert.Contains("PT-OWN-NOTE", block);
            Assert.DoesNotContain("PT-OTHER-NOTE", block);
            Assert.DoesNotContain("PT-PAUSED", block);
            Assert.DoesNotContain("PT-PENDING", block);
            // "Read" (the default) never hands out the note tools.
            Assert.DoesNotContain("[Memory tools:", block);
            Assert.False(scope.ServiceProvider.GetRequiredService<IUserMemoryScope>().AgentNotesEnabled);
        }

        await using (var scope = As(Admin))
        {
            var used = (await Memory(scope).GetOverviewAsync()).Items.Single(m => m.Content.StartsWith("PT-ACTIVE"));
            Assert.NotNull(used.LastUsedAt);

            Assert.Null(await Memory(scope).PrepareTurnAsync(agent, new AssistantBehaviorSettings { UserMemory = "Off" }));
        }

        await using (var scope = As(Admin))
        {
            await Memory(scope).UpdateSettingsAsync(new UserMemorySettingsDto(false, false, true, true));
            Assert.Null(await Memory(scope).PrepareTurnAsync(agent, ReadWrite));
            await Memory(scope).UpdateSettingsAsync(new UserMemorySettingsDto(true, false, true, true));
        }
    }

    [Fact]
    public async Task AgentNotes_AreWrittenOnlyToTheCallingAgentsPartition_WhenBothSidesAllowIt()
    {
        if (!_dockerAvailable) return;
        var agent = await SeedAgentAsync("Note Taker");

        await using (var scope = As(StandardUser))
        {
            // No turn prepared: the tools have no agent to write for.
            Assert.StartsWith("Notes are not available", await Memory(scope).RememberAsync("lang", "Prefers Hindi.", "Preference"));

            // "Read" mode: still no tools.
            await Memory(scope).PrepareTurnAsync(agent, null);
            Assert.StartsWith("Notes are not available", await Memory(scope).RememberAsync("lang", "Prefers Hindi.", "Preference"));
        }

        await using (var scope = As(StandardUser))
        {
            var memory = Memory(scope);
            var block = await memory.PrepareTurnAsync(agent, ReadWrite);
            Assert.Contains("[Memory tools:", block);

            Assert.Equal("Saved note 'preferred_language'.", await memory.RememberAsync("Preferred Language", "Prefers Hindi.", "Preference"));
            Assert.Equal("Updated note 'preferred_language'.", await memory.RememberAsync("preferred_language", "Prefers Tamil.", "Preference"));
            Assert.StartsWith("Not saved: notes can't contain", await memory.RememberAsync("phone", "Call 9876543210.", "Fact"));
            Assert.Equal("Saved note 'team'.", await memory.RememberAsync("team", "Works in stores.", "nonsense-category"));
            Assert.Equal("Forgot note 'team'.", await memory.ForgetAsync("team"));
            Assert.StartsWith("There is no note named", await memory.ForgetAsync("team"));
        }

        await using (var scope = As(StandardUser))
        {
            var notes = (await Memory(scope).GetOverviewAsync()).Items.Where(m => m.AssistantId == agent).ToList();
            var note = Assert.Single(notes);
            Assert.Equal(("preferred_language", "Prefers Tamil.", UserMemorySource.Agent), (note.Key, note.Content, note.Source));
            Assert.Contains((await Memory(scope).GetOverviewAsync()).Partitions, p => p.AssistantId == agent && p.AssistantName == "Note Taker");

            // The user switching agent notes off stops the tools mid-conversation.
            var memory = Memory(scope);
            await memory.PrepareTurnAsync(agent, ReadWrite);
            await memory.UpdateSettingsAsync(new UserMemorySettingsDto(true, false, true, false));
            Assert.StartsWith("Notes are not available", await memory.RememberAsync("late", "Too late.", "Fact"));
            await memory.UpdateSettingsAsync(new UserMemorySettingsDto(true, false, true, true));
        }
    }

    [Fact]
    public async Task ApiKeyRequests_NeitherSeeNorManageTheKeyCreatorsMemory()
    {
        if (!_dockerAvailable) return;
        var agent = await SeedAgentAsync("Integration Agent");
        await using (var scope = As(Admin))
            await Memory(scope).CreateAsync(Save("APIKEY-HIDDEN personal detail."));

        await using (var scope = As(Admin, apiKey: true))
        {
            Assert.Null(await Memory(scope).PrepareTurnAsync(agent, ReadWrite));
            Assert.False(scope.ServiceProvider.GetRequiredService<IUserMemoryScope>().AgentNotesEnabled);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Memory(scope).GetOverviewAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Memory(scope).ClearAsync("all"));
        }
    }

    [Fact]
    public async Task Clear_RemovesOnlyThatPartition_AndAuditsWithoutTheText()
    {
        if (!_dockerAvailable) return;
        var agent = await SeedAgentAsync("Clear Agent");
        await using (var scope = As(StandardUser))
        {
            var memory = Memory(scope);
            await memory.CreateAsync(Save("CLR-PERSONAL keep me."));
            await memory.PrepareTurnAsync(agent, ReadWrite);
            await memory.RememberAsync("clr_note", "CLR-NOTE remove me.", "Fact");
        }

        await using (var scope = As(StandardUser))
        {
            Assert.Equal(1, await Memory(scope).ClearAsync(agent.ToString()));
            var items = (await Memory(scope).GetOverviewAsync()).Items;
            Assert.Contains(items, m => m.Content == "CLR-PERSONAL keep me.");
            Assert.DoesNotContain(items, m => m.AssistantId == agent);
            await Assert.ThrowsAsync<R2WAI.Application.Common.Exceptions.ValidationException>(() => Memory(scope).ClearAsync("everything"));
        }

        await using (var scope = As(StandardUser))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var audits = await db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.EntityType == nameof(UserMemory) && a.UserId == StandardUser)
                .ToListAsync();
            Assert.Contains(audits, a => a.Action == AuditAction.Delete && a.EntityId == agent.ToString());
            Assert.DoesNotContain(audits, a => (a.NewValues ?? "").Contains("CLR-") || (a.OldValues ?? "").Contains("CLR-"));
        }
    }

    [Fact]
    public async Task Learning_AddsPendingSuggestions_SkipsPii_AndNeverOverwritesWhatTheUserWrote()
    {
        if (!_dockerAvailable) return;
        await using (var scope = As(StandardUser))
        {
            var memory = Memory(scope);
            await memory.CreateAsync(Save("Prefers answers in English.", "preferred_language"));
            await memory.UpdateSettingsAsync(new UserMemorySettingsDto(true, true, true, true));
        }

        ScriptedLearningAiService.Reply = """
            <think>scan the exchange</think>
            [{"category":"Profile","key":"job_role","content":"LRN Procurement officer for district stores."},
             {"category":"Preference","key":"preferred_language","content":"Prefers answers in Hindi."},
             {"category":"Fact","key":"contact","content":"LRN reachable at 9876543210."}]
            """;
        var payload = new LearnUserMemoryJobPayload(Tenant, StandardUser, null, null, null,
            "I run procurement for district stores, and answer in Hindi please.", "Noted.");

        // Background jobs run with no signed-in user.
        await using (var scope = As(null))
        {
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().OfType<LearnUserMemoryJobHandler>().Single();
            await handler.HandleAsync(System.Text.Json.JsonSerializer.Serialize(payload), CancellationToken.None);
        }

        await using (var scope = As(StandardUser))
        {
            var items = (await Memory(scope).GetOverviewAsync()).Items;
            var learned = Assert.Single(items, m => m.Source == UserMemorySource.Learned);
            Assert.Equal(("job_role", UserMemoryStatus.PendingReview), (learned.Key, learned.Status));
            Assert.Equal("Prefers answers in English.", items.Single(m => m.Key == "preferred_language").Content);
            Assert.DoesNotContain(items, m => m.Content.Contains("9876543210"));

            // Pending suggestions never reach an agent until approved.
            Assert.DoesNotContain("LRN Procurement", await Memory(scope).PrepareTurnAsync(null, null) ?? "");
            await Memory(scope).ApproveAsync(learned.Id);
        }

        await using (var scope = As(StandardUser))
        {
            Assert.Contains("LRN Procurement", await Memory(scope).PrepareTurnAsync(null, null));

            // Learning switched off: a queued turn learns nothing.
            await Memory(scope).UpdateSettingsAsync(new UserMemorySettingsDto(true, false, true, true));
        }

        ScriptedLearningAiService.Reply = """[{"category":"Goal","content":"LRN-OFF should never be saved."}]""";
        await using (var scope = As(null))
        {
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().OfType<LearnUserMemoryJobHandler>().Single();
            await handler.HandleAsync(System.Text.Json.JsonSerializer.Serialize(payload), CancellationToken.None);
        }
        await using (var scope = As(StandardUser))
        {
            Assert.DoesNotContain((await Memory(scope).GetOverviewAsync()).Items, m => m.Content.StartsWith("LRN-OFF"));
            await Memory(scope).UpdateSettingsAsync(new UserMemorySettingsDto(true, true, true, true));
        }

        // A failed model call (a timeout on a slow local model) is skipped, not retried: the job
        // must complete, or the processor would queue the same slow call again.
        ScriptedLearningAiService.Fail = true;
        try
        {
            await using var scope = As(null);
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().OfType<LearnUserMemoryJobHandler>().Single();
            await handler.HandleAsync(System.Text.Json.JsonSerializer.Serialize(payload), CancellationToken.None);
        }
        finally
        {
            ScriptedLearningAiService.Fail = false;
        }
    }
}

file sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string[] Roles { get; set; } = ["User"];
    public bool IsAuthenticated => UserId is not null;
    public string? IpAddress => null;
    public string? CorrelationId => null;
}

/// <summary>Only GenerateResponseAsync (the learning pass) is used here.</summary>
file sealed class ScriptedLearningAiService : IAIService
{
    public static string Reply { get; set; } = "[]";
    public static bool Fail { get; set; }

    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => Fail ? throw new TaskCanceledException("model timed out") : Task.FromResult(Reply);
    public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, IReadOnlyCollection<Guid>? delegatableAssistantIds = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null,
        bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, IReadOnlyCollection<Guid>? delegatableAssistantIds = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default)
        => throw new NotImplementedException();
}

file sealed class MemoryTestPassThroughEncryption : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
