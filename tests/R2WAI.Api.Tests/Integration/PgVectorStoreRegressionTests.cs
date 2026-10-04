using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;
using R2WAI.Infrastructure.VectorStore;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Runs the real <see cref="PgVectorService"/> against a real Postgres + pgvector.
///
/// Why this file exists: every existing test on the ingestion path faked
/// <see cref="IVectorStoreService"/> with a no-op implementation.
/// <see cref="DocumentServiceKbTenantFilterRegressionTests"/> even documents that in its own header,
/// and its <c>RecordingVectorStore.DeleteVectorsBySourceAsync</c> only counts calls. That made a
/// correctly-wired delete and a silently-never-executed delete indistinguishable in CI, which is how
/// a set of real defects stayed invisible:
/// <list type="bullet">
/// <item>random per-chunk ids turned the "upsert" into an append, so every re-index duplicated a source;</item>
/// <item>document and knowledge-base deletes never touched the vector store at all, so "deleted"
/// text kept matching searches and kept being sent to models;</item>
/// <item>source removal used a top-100 nearest-neighbour scan, so any source with more chunks than
/// that leaked the remainder — permanently, in a collection the tenant was told was empty;</item>
/// <item><c>ApplicationConfiguration.RagThreshold</c> was persisted, validated, versioned and rolled
/// back while no query ever read it.</item>
/// </list>
/// A fake cannot catch any of these. These assertions run the real raw SQL.
/// </summary>
[Trait("Category", "Integration")]
public class PgVectorStoreRegressionTests : IAsyncLifetime
{
    /// <summary>
    /// The seeder's default tenant. Used instead of a random one so the real Tenant and User rows
    /// exist and the KnowledgeBase/Document foreign keys are satisfiable — these tests run against
    /// Postgres, which enforces the FKs that the InMemory-based suites never did.
    /// </summary>
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private bool _dockerAvailable;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        try
        {
            // pgvector/pgvector, not the stock postgres image: PgVectorService issues
            // CREATE EXTENSION vector, which plain postgres:16 does not ship.
            _postgres = new PostgreSqlBuilder()
                .WithImage("pgvector/pgvector:pg16")
                .WithDatabase("r2wai_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            // Mirrors the other Testcontainers suites: skip rather than fail on a Docker-less runner.
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
                builder.UseSetting("VectorStore:VectorSize", "3");

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
                        options.UseNpgsql(_postgres.GetConnectionString());
                        options.ConfigureWarnings(w => w.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });
                    services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                    services.RemoveAll<ICurrentUserService>();
                    services.AddScoped<ICurrentUserService>(_ => new StubCurrentUser(TenantId));

                    services.RemoveAll<IEncryptionService>();
                    services.AddSingleton<IEncryptionService, PassThroughEncryption>();

                    // The real service against the real database. This is the whole point.
                    services.RemoveAll<IVectorStoreService>();
                    services.AddScoped<IVectorStoreService, PgVectorService>();

                    // So the SSRF assertions can prove no request was ever attempted, rather than
                    // inferring it from an error message.
                    services.RemoveAll<IHttpClientFactory>();
                    services.AddSingleton<IHttpClientFactory, CountingHttpClientFactory>();

                    services.RemoveAll<IStorageService>();
                    services.AddSingleton<IStorageService, InMemoryStorage>();

                    services.RemoveAll<IAIService>();
                    services.AddSingleton<IAIService, StubAiService>();
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);

        _userId = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == TenantId)
            .Select(u => u.Id)
            .FirstAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // Re-indexing replaces rather than appends
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpsertingTheSameIdsTwice_UpdatesInPlace_InsteadOfAppending()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();
        var sourceId = Guid.NewGuid();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(sourceId, "sourceId", 5));
            Assert.Equal(5, await CountAsync(collection));

            // Same ids, changed content: exactly what a re-index produces.
            await store.UpsertVectorsAsync(collection,
                BuildChunks(sourceId, "sourceId", 5, suffix: "re-indexed "));

            Assert.Equal(5, await CountAsync(collection));

            var rows = await store.SearchVectorsAsync(collection, [1f, 0f, 0f], 100);
            Assert.Equal(5, rows.Count);
            Assert.All(rows, r => Assert.StartsWith("re-indexed ", PayloadText(r, "content")));
        });
    }

    [Fact]
    public async Task RandomIdsPerChunk_WouldDuplicateOnEveryReindex()
    {
        if (!_dockerAvailable) return;

        // The counterfactual, kept as a test so the suite states what the deterministic id scheme
        // actually prevents rather than merely asserting the new behaviour.
        var collection = NewCollection();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(Guid.NewGuid(), "sourceId", 3));
            await store.UpsertVectorsAsync(collection, BuildChunks(Guid.NewGuid(), "sourceId", 3));

            Assert.Equal(6, await CountAsync(collection));
        });
    }

    // ------------------------------------------------------------------
    // Source deletion is exact, scoped, and not bounded by a search limit
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("sourceId")]   // KnowledgeBaseService's payload key
    [InlineData("documentId")] // DocumentService's payload key
    public async Task DeleteVectorsBySourceAsync_MatchesWhicheverPayloadKeyTheIngestorUsed(string payloadKey)
    {
        if (!_dockerAvailable) return;

        // A filter written for only one key silently no-ops for the other half of the corpus — the
        // exact shape of the original bug, reproduced here inside the fix.
        var collection = NewCollection();
        var sourceId = Guid.NewGuid();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(sourceId, payloadKey, 6));
            Assert.Equal(6, await CountAsync(collection));

            await store.DeleteVectorsBySourceAsync(collection, sourceId);

            Assert.Equal(0, await CountAsync(collection));
        });
    }

    [Fact]
    public async Task DeleteVectorsBySourceAsync_RemovesMoreChunksThanTheOldTop100ScanCouldSee()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();
        var sourceId = Guid.NewGuid();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(sourceId, "sourceId", 150));
            Assert.Equal(150, await CountAsync(collection));

            await store.DeleteVectorsBySourceAsync(collection, sourceId);

            Assert.Equal(0, await CountAsync(collection));
        });
    }

    [Fact]
    public async Task DeleteVectorsBySourceAsync_LeavesOtherSourcesAlone()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();
        var doomed = Guid.NewGuid();
        var survivor = Guid.NewGuid();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(doomed, "sourceId", 4));
            await store.UpsertVectorsAsync(collection, BuildChunks(survivor, "sourceId", 3));

            await store.DeleteVectorsBySourceAsync(collection, doomed);

            Assert.Equal(3, await CountAsync(collection));
            var left = await store.SearchVectorsAsync(collection, [1f, 0f, 0f], 100);
            Assert.All(left, r => Assert.Equal(survivor.ToString(), PayloadText(r, "sourceId")));
        });
    }

    [Fact]
    public async Task DeleteVectorsBySourceAsync_IsScopedToOneCollection()
    {
        if (!_dockerAvailable) return;

        var a = NewCollection();
        var b = NewCollection();
        var shared = Guid.NewGuid();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(a, 3);
            await store.CreateCollectionAsync(b, 3);
            await store.UpsertVectorsAsync(a, BuildChunks(shared, "sourceId", 4));
            await store.UpsertVectorsAsync(b, BuildChunks(shared, "sourceId", 4));

            await store.DeleteVectorsBySourceAsync(a, shared);

            Assert.Equal(0, await CountAsync(a));
            Assert.Equal(4, await CountAsync(b));
        });
    }

    // ------------------------------------------------------------------
    // The relevance floor is enforced in SQL, not just accepted as a parameter
    // ------------------------------------------------------------------

    [Fact]
    public async Task HybridSearchAsync_HonoursTheRelevanceFloor()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, new()
            {
                // Matches the query vector and the query text.
                (Guid.NewGuid(), new[] { 1f, 0f, 0f }, Payload(Guid.NewGuid(), "sourceId", "widget assembly guide")),
                // Matches neither arm.
                (Guid.NewGuid(), new[] { 0f, 1f, 0f }, Payload(Guid.NewGuid(), "sourceId", "unrelated prose about weather")),
            });

            var unfloored = await store.HybridSearchAsync(collection, [1f, 0f, 0f], "widget", 10, 0.7f, 0f);
            Assert.Equal(2, unfloored.Count);

            var floored = await store.HybridSearchAsync(collection, [1f, 0f, 0f], "widget", 10, 0.7f, 0.5f);
            Assert.NotEmpty(floored);
            Assert.All(floored, r => Assert.True(r.Score >= 0.5f, $"expected all results >= 0.5, got {r.Score}"));
            Assert.All(floored, r => Assert.Equal("widget assembly guide", PayloadText(r, "content")));
        });
    }

    [Fact]
    public async Task HybridSearchAsync_AnUnreachableFloorReturnsNothingRatherThanEverything()
    {
        if (!_dockerAvailable) return;

        // Guards the regression that matters most for this parameter: a floor wired to the wrong
        // value (or a >= turned into a >) shows up as "no filtering at all", not as an error.
        var collection = NewCollection();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(Guid.NewGuid(), "sourceId", 3));

            var results = await store.HybridSearchAsync(collection, [1f, 0f, 0f], "chunk 0", 10, 0.7f, 0.99f);

            Assert.Empty(results);
        });
    }

    // ------------------------------------------------------------------
    // Deletes propagate to the vector store
    // ------------------------------------------------------------------

    [Fact]
    public async Task DeleteKnowledgeBaseAsync_PurgesItsCollection()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();
        var kbId = Guid.NewGuid();

        using (var scope = _factory!.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(Guid.NewGuid(), "sourceId", 5));

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var kb = NewKb(kbId, collection);
            db.KnowledgeBases.Add(kb);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory!.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>().DeleteKnowledgeBaseAsync(kbId);
        }

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Soft-deleted, not destroyed...
            Assert.True(await db.KnowledgeBases.IgnoreQueryFilters()
                .AnyAsync(k => k.Id == kbId && k.IsDeleted));

            // ...but its text is gone from the collection. Before the fix only the row was deleted,
            // so every chunk it had ever produced stayed retrievable by a KB in the same collection
            // and kept flowing into model prompts.
            Assert.Equal(0, await CountAsync(collection));
        }
    }

    [Fact]
    public async Task DeleteDocumentAsync_PurgesItsVectors()
    {
        if (!_dockerAvailable) return;

        var collection = NewCollection();
        var documentId = Guid.NewGuid();
        var kbId = Guid.NewGuid();

        using (var scope = _factory!.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, BuildChunks(documentId, "documentId", 5));

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.KnowledgeBases.Add(NewKb(kbId, collection));
            db.Documents.Add(new Document(documentId, TenantId, _userId, "notes.txt", DocumentType.Text, "docs/notes.txt", 100, kbId));
            await db.SaveChangesAsync();
        }

        using (var scope = _factory!.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentService>().DeleteDocumentAsync(documentId);
        }

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.Documents.IgnoreQueryFilters().AnyAsync(d => d.Id == documentId && d.IsDeleted));
            Assert.Equal(0, await CountAsync(collection));
        }
    }

    // ------------------------------------------------------------------
    // SSRF guard on the knowledge-source URL fetch
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/iam/security-credentials/")]
    [InlineData("http://127.0.0.1:5432/")]
    [InlineData("http://localhost/admin")]
    [InlineData("http://10.0.0.5/internal")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://metadata.internal/latest/meta-data/")]
    [InlineData("http://payments.svc.cluster.local/")]
    [InlineData("file:///etc/passwd")]
    public async Task AddSourceAsync_ForABlockedUrl_NeverIssuesAnHttpRequest(string url)
    {
        if (!_dockerAvailable) return;

        // A tenant-supplied URL, fetched server-side, with the body stored as indexed text and later
        // fed to models. Without a guard this is a data-exfiltration primitive aimed at the cloud
        // metadata endpoint. Every other outbound fetch in the codebase already goes through
        // EgressGuard; this one was the remaining gap, and guarding a separate "test connection"
        // button is worthless — the check has to sit on the real dispatch.
        var http = (CountingHttpClientFactory)_factory!.Services.GetRequiredService<IHttpClientFactory>();
        http.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>();

        var kb = NewKb(Guid.NewGuid(), NewCollection());
        db.KnowledgeBases.Add(kb);
        await db.SaveChangesAsync();

        var source = await service.AddSourceAsync(kb.Id, "Url", null, url, null);

        Assert.Equal(0, http.Creations);
        Assert.Equal("Failed", source.Status);
        Assert.Contains("not an allowed ingestion target", source.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSourceAsync_ForAPublicUrl_StillReachesTheNetwork()
    {
        if (!_dockerAvailable) return;

        // The counterfactual, so the guard above cannot be "satisfied" by breaking ingestion.
        var http = (CountingHttpClientFactory)_factory!.Services.GetRequiredService<IHttpClientFactory>();
        http.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>();
        var store = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();

        var collection = NewCollection();
        // The real bug this test was actually hitting: unlike every sibling test in this file that
        // expects a successful index (e.g. DeleteDocumentAsync_PurgesItsVectors above), this one
        // never created the pgvector collection before indexing into it — AddSourceAsync's upsert
        // then failed for real (no such collection), which surfaced as source.Status == "Failed",
        // not as a network problem. The fetch/egress/embedding path was never the issue.
        await store.CreateCollectionAsync(collection, 3);
        var kb = NewKb(Guid.NewGuid(), collection);
        db.KnowledgeBases.Add(kb);
        await db.SaveChangesAsync();

        // example.com/docs 404s (the domain serves nothing but its root page) — the root path is
        // the one reliably-real, indexable page on this domain, which is exactly what this test
        // (the counterfactual to the SSRF-blocked-URL test above) needs: a genuine public fetch
        // that actually succeeds, not merely a URL that passes the egress guard.
        var source = await service.AddSourceAsync(kb.Id, "Url", null, "https://example.com/", null);

        Assert.True(http.Creations >= 1, "expected the public URL to actually be fetched");
        Assert.Equal("Indexed", source.Status);
        Assert.Equal(1, source.ChunkCount);
    }

    // ------------------------------------------------------------------
    // The batched upsert is a real single statement, not a failed COPY
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpsertVectorsAsync_ReportsAPartiallyFailedBatchInsteadOfClaimingSuccess()
    {
        if (!_dockerAvailable) return;

        // The old implementation's COPY path failed on every call with "vector must have at least
        // 1 dimension" (Npgsql has no pgvector mapping, so float[] was written as a zero-dimension
        // vector) and was silently downgraded to one INSERT per vector. A plain correctness test
        // cannot see that, because the fallback produced correct rows.
        //
        // This can. Callers mark the source Indexed with the chunk count they *asked* for, so an
        // upsert that quietly drops points leaves the collection permanently disagreeing with the
        // source's own bookkeeping, with nothing downstream able to tell.
        var collection = NewCollection();

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);

            var good = BuildChunks(Guid.NewGuid(), "sourceId", 4);
            var bad = new List<(Guid, float[], Dictionary<string, object>)>
            {
                // Right shape, wrong dimension for this collection.
                (DeterministicChunkId.ForSourceChunk(Guid.NewGuid(), 0), new[] { 1f, 0f },
                    Payload(Guid.NewGuid(), "sourceId", "wrong dimension")),
            };

            await Assert.ThrowsAnyAsync<Exception>(
                () => store.UpsertVectorsAsync(collection, good.Concat(bad).ToList()));

            // The atomic batch rolled back, so what survives is only the retry of the good points —
            // never a silently-partial success.
            Assert.True(await CountAsync(collection) <= good.Count);
        });
    }

    [Fact]
    public async Task UpsertVectorsAsync_PreservesVectorComponentsExactly()
    {
        if (!_dockerAvailable) return;

        // Proves the pgvector text round-trip and the invariant-culture formatting: a locale with a
        // decimal comma would turn "[0.1,0.2]" into "[0,1,0,2]" — a different vector in a different
        // dimension, which is a silent data-corruption bug rather than an exception.
        var collection = NewCollection();
        var id = DeterministicChunkId.ForSourceChunk(Guid.NewGuid(), 0);

        await WithStoreAsync(async store =>
        {
            await store.CreateCollectionAsync(collection, 3);
            await store.UpsertVectorsAsync(collection, new()
            {
                (id, new[] { 0.125f, -0.5f, 0.0625f }, Payload(Guid.NewGuid(), "sourceId", "precise")),
            });

            Assert.Equal(1, await CountAsync(collection));

            var actual = await ScalarAsync<string>(
                "SELECT embedding::text FROM vector_embeddings WHERE id = @p", id);
            Assert.Equal("[0.125,-0.5,0.0625]", actual);
        });
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string NewCollection() => $"test_{Guid.NewGuid():N}";

    /// <summary>
    /// Payload values come back as JsonElement (the store deserializes the jsonb into
    /// Dictionary&lt;string, object?&gt;), so every read has to unwrap rather than cast.
    /// </summary>
    private static string PayloadText(VectorSearchResult result, string key)
        => result.Payload is not null && result.Payload.TryGetValue(key, out var v) && v is not null
            ? v.ToString()!
            : throw new Xunit.Sdk.XunitException($"payload key '{key}' missing");

    private KnowledgeBase NewKb(Guid id, string collection)
    {
        var kb = new KnowledgeBase(id, TenantId, _userId, "Test KB");
        kb.ConfigureEmbedding("text-embedding-3-small", 1000, 200, collection);
        return kb;
    }

    private async Task WithStoreAsync(Func<IVectorStoreService, Task> body)
    {
        using var scope = _factory!.Services.CreateScope();
        await body(scope.ServiceProvider.GetRequiredService<IVectorStoreService>());
    }

    private async Task<int> CountAsync(string collection)
    {
        return await ScalarAsync<int>(
            "SELECT count(*)::int FROM vector_embeddings WHERE collection_name = @p", collection);
    }

    private async Task<T> ScalarAsync<T>(string sql, object param)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("p", param);
        return (T)Convert.ChangeType(await cmd.ExecuteScalarAsync(), typeof(T));
    }


    /// <summary>
    /// One-hot vectors along the first axis, so cosine scores are predictable enough to assert a
    /// relevance floor against. Ids are derived from (sourceId, chunkIndex) the way the production
    /// ingestion paths do, which is what makes a re-index collide instead of append.
    /// </summary>
    private static List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)> BuildChunks(
        Guid sourceId, string payloadKey, int count, string suffix = "")
    {
        var points = new List<(Guid, float[], Dictionary<string, object>)>(count);
        for (var i = 0; i < count; i++)
        {
            points.Add((DeriveChunkId(sourceId, i),
                new[] { 1f, (float)i / count, 0f },
                Payload(sourceId, payloadKey, $"{suffix}chunk {i}")));
        }
        return points;
    }

    private static Guid DeriveChunkId(Guid sourceId, int chunkIndex)
        => DeterministicChunkId.ForSourceChunk(sourceId, chunkIndex);

    private static Dictionary<string, object> Payload(Guid sourceId, string payloadKey, string content)
    {
        // Both real ingestion paths write the id as .ToString(), i.e. the dashed "D" form, and
        // DeleteVectorsBySourceAsync matches that exact form.
        return new Dictionary<string, object>
        {
            ["content"] = content,
            ["chunkIndex"] = 0,
            ["source"] = $"doc_{sourceId}",
            [payloadKey] = sourceId.ToString(),
        };
    }

    private sealed class StubCurrentUser(Guid tenantId) : ICurrentUserService
    {
        // TenantId only. The tenant is what the fail-closed query filter needs, and AuditLog rows
        // are emitted for every SaveChanges once an ambient tenant exists — but AuditLog.UserId is a
        // real foreign key, so a non-null ambient user id would fail the seed before that user row
        // exists. Null is both legal and the honest state for a background/host process.
        public Guid? UserId => null;
        public Guid? TenantId { get; } = tenantId;
        public string[] Roles { get; } = ["Admin"];
        public bool IsAuthenticated => true;
        public string? IpAddress => "127.0.0.1";
        public string? CorrelationId => Guid.NewGuid().ToString();
    }

    private sealed class PassThroughEncryption : IEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
    }

    private sealed class InMemoryStorage : IStorageService
    {
        public Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null, CancellationToken ct = default) =>
            Task.FromResult(fileName);
        public Task<Stream> DownloadFileAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test document")));
        public Task DeleteFileAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> FileExistsAsync(string path, CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> GetFileUrlAsync(string path, CancellationToken ct = default) => Task.FromResult(path);
    }

    /// <summary>Fixed-width embeddings, so a vector(3) collection is enough for these tests.</summary>
    private sealed class StubAiService : IAIService
    {
        private static IReadOnlyList<float> Embed() => new float[] { 0.1f, 0.2f, 0.3f };
        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default) => Task.FromResult(Embed());
        public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(texts.Select(_ => Embed()).ToList());

        public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class CountingHttpClientFactory : IHttpClientFactory
    {
        private int _creations;
        public int Creations => Volatile.Read(ref _creations);
        public void Reset() => Interlocked.Exchange(ref _creations, 0);

        public HttpClient CreateClient(string name)
        {
            Interlocked.Increment(ref _creations);
            return new HttpClient(new StubHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body><p>on-topic public documentation</p></body></html>"),
                });
        }
    }
}
