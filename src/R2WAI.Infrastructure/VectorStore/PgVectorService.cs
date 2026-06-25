using System.Text.Json;
using Npgsql;

namespace R2WAI.Infrastructure.VectorStore;

public class PgVectorService : IVectorStoreService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PgVectorService> _logger;
    private readonly int _defaultVectorSize;

    private static bool _extensionInitialized;

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

    public async Task CreateCollectionAsync(string collectionName, int vectorSize, CancellationToken ct = default)
    {
        await EnsurePgVectorExtensionAsync(ct);

        var sql = """
            CREATE TABLE IF NOT EXISTS vector_embeddings (
                id UUID PRIMARY KEY,
                collection_name TEXT NOT NULL,
                embedding vector($1),
                payload JSONB,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )
            """;

        await _context.Database.ExecuteSqlRawAsync(sql, [vectorSize], ct);

        var idxSql = """
            CREATE INDEX IF NOT EXISTS idx_ve_collection_name
                ON vector_embeddings (collection_name)
            """;

        await _context.Database.ExecuteSqlRawAsync(idxSql, ct);

        _logger.LogInformation("Ensured vector_embeddings table with size {Size} for collection {Collection}", vectorSize, collectionName);
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

        try
        {
            using var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
            await conn.OpenAsync(ct);

            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY vector_embeddings (id, collection_name, embedding, payload) FROM STDIN (FORMAT BINARY)", ct);

            foreach (var (id, vector, payload) in points)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(id, ct);
                await writer.WriteAsync(collectionName, ct);
                await writer.WriteAsync(vector, ct);
                await writer.WriteAsync(JsonSerializer.Serialize(payload), NpgsqlTypes.NpgsqlDbType.Jsonb, ct);
            }

            await writer.CompleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert {Count} vectors to {Collection}, falling back to row-by-row", points.Count, collectionName);

            foreach (var (id, vector, payload) in points)
            {
                try
                {
                    var sql = """
                        INSERT INTO vector_embeddings (id, collection_name, embedding, payload)
                        VALUES ({0}, {1}, {2}::vector, {3}::jsonb)
                        ON CONFLICT (id) DO UPDATE SET
                            embedding = EXCLUDED.embedding,
                            payload = EXCLUDED.payload
                        """;

                    await _context.Database.ExecuteSqlRawAsync(sql,
                        [id, collectionName, vector, JsonSerializer.Serialize(payload)], ct);
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "Failed to upsert vector {Id}", id);
                }
            }
        }

        _logger.LogInformation("Upserted {Count} vectors to {Collection}", points.Count, collectionName);
    }

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
        CancellationToken ct = default)
    {
        try
        {
            var sql = """
                WITH vector_results AS (
                    SELECT id, 1 - (embedding <=> {0}::vector) AS vector_score, payload
                    FROM vector_embeddings
                    WHERE collection_name = {1}
                    ORDER BY embedding <=> {0}::vector
                    LIMIT {2} * 2
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
                    LIMIT {2} * 2
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
                ORDER BY combined_score DESC
                LIMIT {2}
                """;

            var query = _context.Database.SqlQueryRaw<VectorSearchDbResult>(
                sql, new object[] { queryVector, collectionName, limit, queryText, vectorWeight });
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
            return await SearchVectorsAsync(collectionName, queryVector, limit, ct);
        }
    }

    private async Task EnsurePgVectorExtensionAsync(CancellationToken ct)
    {
        if (_extensionInitialized) return;

        try
        {
            await _context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector", ct);
            _extensionInitialized = true;
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
