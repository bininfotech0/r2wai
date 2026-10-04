using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.VectorStore;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// A repository research pass (auditing RAG for the opposite, leak, direction — found none) turned
/// up an adjacent fail-closed-swallowing bug: DocumentService.ProcessDocumentAsync's KnowledgeBase
/// lookup had no expectedTenantId-or-ambient handling, unlike the Document fetch two lines above it
/// which already had it. IndexDocumentJobHandler's background call (expectedTenantId set, no
/// ambient tenant) always got a null `kb` under the fail-closed filter, so the whole embed/upsert
/// step silently no-opped (caught by the method's own broad try/catch, logged as a warning, no
/// exception surfaces) — meaning a background-processed document with a linked KnowledgeBase never
/// actually got indexed into it. This is the InMemory-EF-only-because-the-bug-is-in-an-EF-LINQ-
/// query variant of the same test shape as BackgroundJobLeaseReclaimRegressionTests — no Postgres/
/// Testcontainers needed here, since PgVectorService's raw-SQL side is never exercised (it's faked
/// out) and the fix itself is a plain EF LINQ predicate.
/// </summary>
[Trait("Category", "Integration")]
public class DocumentServiceKbTenantFilterRegressionTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private WebApplicationFactory<Program>? _factory;
    private readonly string _dbName = $"DocumentServiceKbTest_{Guid.NewGuid()}";

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Authentication:Jwt:SecretKey", "TestingSecretKeyForIntegrationTestsThatIsLongEnough!");
                builder.UseSetting("ConnectionStrings:Redis", "");
                builder.UseSetting("Cache:Redis:ConnectionString", "");
                Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "lEq8IPYv6Hd2+m2OX+kjWGsx4NIhsX4COeYDKiR0D2M=");

                builder.ConfigureServices(services =>
                {
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        d.ServiceType.FullName?.Contains("DbContextOptions") == true ||
                        d.ServiceType == typeof(ApplicationDbContext) ||
                        d.ImplementationType == typeof(ApplicationDbContext) ||
                        d.ServiceType == typeof(ITenantDbContext) ||
                        d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    ).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    var npgsqlDescriptors = services.Where(d =>
                        d.ServiceType.FullName?.Contains("Npgsql") == true ||
                        d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    ).ToList();
                    foreach (var d in npgsqlDescriptors) services.Remove(d);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(_dbName);
                        options.ConfigureWarnings(w =>
                        {
                            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                        });
                    });
                    services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                    // Real PgVectorService needs a real Postgres connection for its raw-SQL calls —
                    // not what this test is about. RecordingVectorStore lets the test observe
                    // whether UpsertVectorsAsync was actually reached, which is the whole point.
                    services.RemoveAll<IVectorStoreService>();
                    services.AddSingleton<IVectorStoreService, RecordingVectorStore>();

                    services.RemoveAll<IEncryptionService>();
                    services.AddSingleton<IEncryptionService, NoOpEncryptionService>();

                    // ProcessDocumentAsync downloads the real file and generates real embeddings
                    // before it ever reaches the KB lookup this test is actually about — fake both
                    // so the flow reaches that lookup instead of failing earlier for an unrelated
                    // reason (no real storage backend/AI provider configured in this test host).
                    services.RemoveAll<IStorageService>();
                    services.AddSingleton<IStorageService, FakeStorageService>();
                    services.RemoveAll<IAIService>();
                    services.AddSingleton<IAIService, FakeAiService>();
                });
            });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
    }

    private async Task<(Guid DocumentId, Guid KnowledgeBaseId)> SeedDocumentWithKbAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var kb = new KnowledgeBase(Guid.NewGuid(), TenantId, Guid.NewGuid(), "Test KB");
        kb.ConfigureEmbedding("text-embedding-3-small", 1000, 200, $"kb_{kb.Id:N}");
        db.Set<KnowledgeBase>().Add(kb);

        var document = new Document(Guid.NewGuid(), TenantId, Guid.NewGuid(), "notes.txt", DocumentType.Text, "notes.txt", 100, kb.Id);
        db.Set<Document>().Add(document);

        await db.SaveChangesAsync();
        return (document.Id, kb.Id);
    }

    [Fact]
    public async Task ProcessDocumentAsync_BackgroundCallWithNoAmbientTenant_StillIndexesIntoTheLinkedKnowledgeBase()
    {
        var (documentId, kbId) = await SeedDocumentWithKbAsync();

        using var scope = _factory!.Services.CreateScope();
        var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

        // expectedTenantId set, no ambient HttpContext/ICurrentUserService.TenantId — exactly
        // IndexDocumentJobHandler's own call shape (BackgroundJobProcessor has no HttpContext).
        await documentService.ProcessDocumentAsync(documentId, CancellationToken.None, expectedTenantId: TenantId);

        RecordingVectorStore.UpsertCalls.TryGetValue(kbId, out var callCount);
        Assert.True(callCount > 0, "Expected UpsertVectorsAsync to be called for this document's KnowledgeBase collection — the kb lookup silently returned null before this fix.");

        using var verifyScope = _factory!.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await db.Set<Document>().IgnoreQueryFilters().FirstAsync(d => d.Id == documentId);
        Assert.Equal(DocumentStatus.Ready, document.Status);
    }
}

file sealed class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}

file sealed class FakeStorageService : IStorageService
{
    public Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null, CancellationToken ct = default) =>
        Task.FromResult(fileName);
    public Task<Stream> DownloadFileAsync(string path, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Regression test document content.")));
    public Task DeleteFileAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> FileExistsAsync(string path, CancellationToken ct = default) => Task.FromResult(true);
    public Task<string> GetFileUrlAsync(string path, CancellationToken ct = default) => Task.FromResult(path);
}

file sealed class FakeAiService : IAIService
{
    public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(texts.Select(_ => (IReadOnlyList<float>)new float[] { 0.1f, 0.2f, 0.3f }).ToList());
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<float>>(new float[] { 0.1f, 0.2f, 0.3f });

    public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

file sealed class RecordingVectorStore : IVectorStoreService
{
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> UpsertCalls = new();
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> DeleteSourceCalls = new();

    public Task CreateCollectionAsync(string collectionName, int? vectorSize = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default) => Task.CompletedTask;

    public Task UpsertVectorsAsync(string collectionName, List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)> points, CancellationToken ct = default)
    {
        // collectionName is "kb_{id:N}" (KnowledgeBaseService.CreateKnowledgeBaseAsync's own
        // convention) — parse the id back out so the test can key its assertion by KB id.
        if (collectionName.StartsWith("kb_", StringComparison.Ordinal)
            && Guid.TryParseExact(collectionName["kb_".Length..], "N", out var kbId))
            UpsertCalls.AddOrUpdate(kbId, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }

    public Task<List<VectorSearchResult>> SearchVectorsAsync(string collectionName, float[] queryVector, int limit = 10, CancellationToken ct = default) =>
        Task.FromResult(new List<VectorSearchResult>());
    public Task<List<VectorSearchResult>> HybridSearchAsync(string collectionName, float[] queryVector, string queryText, int limit = 10, float vectorWeight = 0.7f, float minRelevance = 0f, CancellationToken ct = default) =>
        Task.FromResult(new List<VectorSearchResult>());
    public Task DeleteVectorsAsync(string collectionName, List<Guid> pointIds, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteVectorsBySourceAsync(string collectionName, Guid sourceId, CancellationToken ct = default)
    {
        // Recorded, not silently swallowed: this fake used to no-op every delete, which is precisely
        // why a document/KB delete that left its vectors behind was invisible to CI — a genuine fix
        // and the original leak looked identical to it.
        DeleteSourceCalls.AddOrUpdate(sourceId, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}
