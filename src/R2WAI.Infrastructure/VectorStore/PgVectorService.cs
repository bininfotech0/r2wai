using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace R2WAI.Infrastructure.VectorStore;

public class PgVectorService : IVectorStoreService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PgVectorService> _logger;
    private readonly int _defaultVectorSize;

    // Keyed by connection string, not a bare bool. A process-wide flag meant CREATE EXTENSION ran
    // exactly once per process, so the *second* database this process ever touched — a restored
    // database, a second environment pointed at the same app, a per-tenant shard — never got the
    // vector type and every subsequent CreateCollectionAsync failed with
    // "type \"vector\" does not exist".
    private static readonly ConcurrentDictionary<string, bool> _extensionsEnsured = new();

    public PgVectorService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<PgVectorService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _defaultVectorSize = int.Parse(_configuration["VectorStore:VectorSize"] ?? "1536");
    }

    public async Task CreateCollectionAsync(string collectionName, int? vectorSize = null, CancellationToken ct = default)
    {
        await EnsurePgVectorExtensionAsync(ct);

        var resolvedVectorSize = vectorSize ?? _defaultVectorSize;

        // pgvector's type modifier (the embedding dimension) must be a literal in the DDL —
        // Postgres rejects a bound parameter there ("type modifiers must be simple constants
        // or identifiers"). vectorSize is an int from internal config, not user input, so
        // interpolating it directly is safe.
        var sql = $"""
            CREATE TABLE IF NOT EXISTS vector_embeddings (
                id UUID PRIMARY KEY,
                collection_name TEXT NOT NULL,
                embedding vector({resolvedVectorSize}),
                payload JSONB,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )
            """;

        await _context.Database.ExecuteSqlRawAsync(sql, ct);

        var idxSql = """
            CREATE INDEX IF NOT EXISTS idx_ve_collection_name
                ON vector_embeddings (collection_name)
            """;

        await _context.Database.ExecuteSqlRawAsync(idxSql, ct);

        // Every search does a plain `ORDER BY embedding <=> $1` over the whole collection, and
        // CreateCollectionAsync is the only place the table shape is guaranteed to be in one place.
        // Without an ANN index pgvector falls back to an exact sequential scan, so search cost grew
        // linearly with corpus size — the "500K embeddings" ceiling called out in the roadmap is a
        // row-count limit only, not a query-plan one. Not built CONCURRENTLY: this runs right after a
        // collection is created (so the table is near-empty) and CONCURRENTLY cannot run inside the
        // transaction EF opens for the enclosing SaveChanges.
        var hnswSql = $"""
            CREATE INDEX IF NOT EXISTS idx_ve_embedding_hnsw
                ON vector_embeddings USING hnsw (embedding vector_cosine_ops)
                WITH (m = 16, ef_construction = 64)
            """;

        try
        {
            await _context.Database.ExecuteSqlRawAsync(hnswSql, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal on purpose: the extension may be an older pgvector build without hnsw, and
            // an exact-scan fallback is correct-but-slower. Losing the index must not stop a KB
            // from being created.
            _logger.LogWarning(ex, "Could not create HNSW index for vector_embeddings at size {Size} — searches will fall back to an exact scan", resolvedVectorSize);
        }

        // Backs DeleteVectorsBySourceAsync, whose predicate is
        //   payload->>'sourceId' = $1 OR payload->>'documentId' = $1
        //
        // These are plain btree expression indexes, deliberately NOT a GIN index with
        // jsonb_path_ops. jsonb_path_ops only accelerates @>, @?, @@, ?| and ?& — it cannot serve
        // a ->> equality comparison, so a GIN index here looked like it covered the delete while
        // leaving it a sequential scan. The earlier comment claimed otherwise, and because the
        // CREATE was wrapped in a catch-and-log, nothing ever noticed.
        foreach (var (indexName, expression) in new[]
                 {
                     ("idx_ve_payload_source_id", "((payload->>'sourceId'))"),
                     ("idx_ve_payload_document_id", "((payload->>'documentId'))"),
                 })
        {
            var payloadIdxSql = $"""
                CREATE INDEX IF NOT EXISTS {indexName}
                    ON vector_embeddings {expression}
                """;

            try
            {
                await _context.Database.ExecuteSqlRawAsync(payloadIdxSql, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create {Index} on vector_embeddings.payload — source deletes will fall back to a sequential scan", indexName);
            }
        }

        _logger.LogInformation("Ensured vector_embeddings table with size {Size} for collection {Collection}", resolvedVectorSize, collectionName);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        var sql = "DELETE FROM vector_embeddings WHERE collection_name = {0}";
        await _context.Database.ExecuteSqlRawAsync(sql, [collectionName], ct);
        _logger.LogInformation("Deleted vectors for collection {Collection}", collectionName);
    }

    public async Task UpsertVectorsAsync(
        string collectionName,
        List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)> points,
        CancellationToken ct = default)
    {
        if (points.Count == 0) return;

        // One statement for the whole batch, with the conflict handling done by Postgres itself.
        //
        // This previously went through BeginBinaryImportAsync (COPY ... FORMAT BINARY), which
        // *always* failed: Npgsql has no pgvector type mapping, so a float[] was written to the
        // `vector` column as a zero-dimension vector and pgvector rejected every row with
        // "vector must have at least 1 dimension". The failure was swallowed by the catch and
        // silently downgraded to one INSERT per vector, so the "fast path" was dead code that cost
        // a wasted round trip on top of the slow one.
        //
        // COPY also could not have been correct for the common case even if it had worked: COPY has
        // no ON CONFLICT, so a re-index (which now collides by design, see DeterministicChunkId)
        // would abort the whole batch on the first duplicate. Vectors therefore travel as their
        // pgvector text form ("[1,0,0]") and are cast server-side.
        var sql = """
            INSERT INTO vector_embeddings (id, collection_name, embedding, payload)
            SELECT u.id, u.collection_name, u.embedding::vector, u.payload::jsonb
            FROM unnest({0}::uuid[], {1}::text[], {2}::text[], {3}::text[])
                 AS u(id, collection_name, embedding, payload)
            ON CONFLICT (id) DO UPDATE SET
                -- id is the primary key of the whole table, not per collection, and
                // DeterministicChunkId derives it from (sourceId, chunkIndex) without the collection.
                -- A point that is re-indexed into a different collection (a KB whose collection was
                -- re-provisioned, a source moved between KBs) must therefore be moved too — leaving
                -- this column out silently keeps the old copy in the old collection and drops the
                // new one entirely.
                collection_name = EXCLUDED.collection_name,
                embedding = EXCLUDED.embedding,
                payload   = EXCLUDED.payload
            """;

        try
        {
            await _context.Database.ExecuteSqlRawAsync(sql, new object[]
            {
                ArrayParam(NpgsqlDbType.Uuid, points.Select(p => p.Id).ToArray()),
                ArrayParam(NpgsqlDbType.Text, Enumerable.Repeat(collectionName, points.Count).ToArray()),
                ArrayParam(NpgsqlDbType.Text, points.Select(p => ToPgVectorText(p.Vector)).ToArray()),
                ArrayParam(NpgsqlDbType.Text, points.Select(p => JsonSerializer.Serialize(p.Payload)).ToArray()),
            }, ct);

            _logger.LogInformation("Upserted {Count} vectors to {Collection}", points.Count, collectionName);
        }
        catch (Exception ex)
        {
            // Last resort, one statement per vector. Genuinely reached only if the batch statement
            // itself is unusable (e.g. a pgvector build without the operators this needs).
            _logger.LogError(ex, "Batched upsert of {Count} vectors to {Collection} failed, falling back to row-by-row", points.Count, collectionName);

            var failures = new List<Exception>();
            foreach (var (id, vector, payload) in points)
            {
                try
                {
                    var rowSql = """
                        INSERT INTO vector_embeddings (id, collection_name, embedding, payload)
                        VALUES ({0}, {1}, {2}::vector, {3}::jsonb)
                        ON CONFLICT (id) DO UPDATE SET
                            collection_name = EXCLUDED.collection_name,
                            embedding = EXCLUDED.embedding,
                            payload   = EXCLUDED.payload
                        """;

                    await _context.Database.ExecuteSqlRawAsync(rowSql,
                        [id, collectionName, ToPgVectorText(vector), JsonSerializer.Serialize(payload)], ct);
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "Failed to upsert vector {Id}", id);
                    failures.Add(ex2);
                }
            }

            // Surfaced rather than swallowed. The callers mark the source Indexed with the chunk
            // count they *asked* for, so returning normally after a partial write leaves the
            // collection disagreeing with the source's own bookkeeping — and nothing downstream can
            // tell that apart from a clean index.
            if (failures.Count > 0)
                throw new InvalidOperationException(
                    $"Vector upsert into '{collectionName}' failed for {failures.Count} of {points.Count} points.",
                    new AggregateException(failures));
        }
    }

    private static NpgsqlParameter ArrayParam(NpgsqlDbType elementType, Array value)
        => new() { NpgsqlDbType = NpgsqlDbType.Array | elementType, Value = value };

    /// <summary>pgvector's text input form, e.g. "[0.1,0.2,0.3]". Invariant culture: a decimal
    /// comma here would silently corrupt the vector, and this runs on servers with any locale.</summary>
    private static string ToPgVectorText(float[] vector)
        => "[" + string.Join(",", vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";

    public async Task<List<VectorSearchResult>> SearchVectorsAsync(
        string collectionName,
        float[] queryVector,
        int limit = 10,
        CancellationToken ct = default)
    {
        try
        {
            var sql = """
                SELECT id, 1 - (embedding <=> {0}::vector) AS score, payload
                FROM vector_embeddings
                WHERE collection_name = {1}
                ORDER BY embedding <=> {0}::vector
                LIMIT {2}
                """;

            var query = _context.Database.SqlQueryRaw<VectorSearchDbResult>(
                sql, new object[] { queryVector, collectionName, limit });
            var results = await query.ToListAsync(ct);

            return results.Select(r => new VectorSearchResult
            {
                Id = r.Id,
                Score = (float)r.Score,
                Payload = r.Payload is null
                    ? []
                    : JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vectors in {Collection}", collectionName);
            return [];
        }
    }

    public async Task DeleteVectorsAsync(string collectionName, List<Guid> pointIds, CancellationToken ct = default)
    {
        if (pointIds.Count == 0) return;

        try
        {
            var sql = "DELETE FROM vector_embeddings WHERE collection_name = @collectionName AND id = ANY(@ids)";
            await _context.Database.ExecuteSqlRawAsync(
                sql,
                new NpgsqlParameter("@collectionName", collectionName),
                new NpgsqlParameter("@ids", pointIds.ToArray()),
                ct);
            _logger.LogInformation("Deleted {Count} vectors from {Collection}", pointIds.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vectors from {Collection}", collectionName);
        }
    }

    public async Task<List<VectorSearchResult>> HybridSearchAsync(
        string collectionName,
        float[] queryVector,
        string queryText,
        int limit = 10,
        float vectorWeight = 0.7f,
        float minRelevance = 0f,
        CancellationToken ct = default)
    {
        try
        {
            // Over-fetch each arm so the blend still has enough candidates after minRelevance drops
            // the weak ones. minRelevance is a bound parameter ({5}), not interpolated.
            var sql = """
                WITH vector_results AS (
                    SELECT id, 1 - (embedding <=> {0}::vector) AS vector_score, payload
                    FROM vector_embeddings
                    WHERE collection_name = {1}
                    ORDER BY embedding <=> {0}::vector
                    LIMIT {2} * 4
                ),
                text_results AS (
                    SELECT id,
                           ts_rank(to_tsvector('english', COALESCE(payload->>'content', '')),
                                   plainto_tsquery('english', {3})) AS text_score,
                           payload
                    FROM vector_embeddings
                    WHERE collection_name = {1}
                      AND to_tsvector('english', COALESCE(payload->>'content', ''))
                          @@ plainto_tsquery('english', {3})
                    LIMIT {2} * 4
                ),
                combined AS (
                    SELECT COALESCE(v.id, t.id) AS id,
                           COALESCE(v.vector_score, 0) * {4} +
                           COALESCE(t.text_score, 0) * (1 - {4}) AS combined_score,
                           COALESCE(v.payload, t.payload) AS payload
                    FROM vector_results v
                    FULL OUTER JOIN text_results t ON v.id = t.id
                )
                SELECT id, combined_score AS score, payload
                FROM combined
                WHERE combined_score >= {5}
                ORDER BY combined_score DESC
                LIMIT {2}
                """;

            var query = _context.Database.SqlQueryRaw<VectorSearchDbResult>(
                sql, new object[] { queryVector, collectionName, limit, queryText, vectorWeight, minRelevance });
            var results = await query.ToListAsync(ct);

            return results.Select(r => new VectorSearchResult
            {
                Id = r.Id,
                Score = (float)r.Score,
                Payload = r.Payload is null
                    ? []
                    : JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid search failed, falling back to vector-only search");
            var fallback = await SearchVectorsAsync(collectionName, queryVector, limit, ct);
            return minRelevance > 0f
                ? fallback.Where(r => r.Score >= minRelevance).ToList()
                : fallback;
        }
    }

    public async Task DeleteVectorsBySourceAsync(string collectionName, Guid sourceId, CancellationToken ct = default)
    {
        try
        {            // A "source" of chunks is either a KnowledgeBaseSource or a Document, and the two
            // ingestion paths tag their payload with different keys ("sourceId" vs "documentId").
            // Both write the id as .ToString(), i.e. the dashed "D" form, so the jsonb ->>
            // comparison below is an exact match against existing rows.
            // Deliberately not a nearest-neighbour scan: that only ever saw the top N matches, so a
            // source with more chunks than the scan limit leaked the remainder.
            var sql = """
                DELETE FROM vector_embeddings
                WHERE collection_name = {0}
                  AND (payload->>'sourceId' = {1} OR payload->>'documentId' = {1})
                """;
            var deleted = await _context.Database.ExecuteSqlRawAsync(sql, [collectionName, sourceId.ToString()], ct);
            _logger.LogInformation("Deleted {Count} vectors for source {SourceId} from {Collection}", deleted, sourceId, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vectors for source {SourceId} from {Collection}", sourceId, collectionName);
            throw;
        }
    }

    private async Task EnsurePgVectorExtensionAsync(CancellationToken ct)
    {
        var connectionString = _context.Database.GetConnectionString();
        if (connectionString is not null &&
            _extensionsEnsured.TryGetValue(connectionString, out var done) && done)
            return;

        try
        {
            await _context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector", ct);
            if (connectionString is not null) _extensionsEnsured[connectionString] = true;
            _logger.LogInformation("pgvector extension enabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable pgvector extension");
            throw;
        }
    }

    private sealed class VectorSearchDbResult
    {
        public Guid Id { get; set; }
        public double Score { get; set; }
        public string? Payload { get; set; }
    }
}
