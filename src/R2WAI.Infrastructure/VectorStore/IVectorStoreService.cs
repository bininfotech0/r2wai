namespace R2WAI.Infrastructure.VectorStore;

public interface IVectorStoreService
{
    /// <param name="vectorSize">
    /// Explicit embedding dimension for this collection. Null uses the deployment's configured
    /// default ("VectorStore:VectorSize" — the actual output dimension of whatever embedding
    /// model "AI:Provider" resolves to, not necessarily OpenAI's 1536).
    /// </param>
    Task CreateCollectionAsync(string collectionName, int? vectorSize = null, CancellationToken ct = default);
    Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default);
    Task UpsertVectorsAsync(string collectionName, List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)> points, CancellationToken ct = default);
    Task<List<VectorSearchResult>> SearchVectorsAsync(string collectionName, float[] queryVector, int limit = 10, CancellationToken ct = default);

    /// <param name="vectorWeight">
    /// Blend weight between the vector and full-text scores, 0..1. Not a relevance floor.
    /// </param>
    /// <param name="minRelevance">
    /// Floor on the blended <c>combined_score</c>; results scoring below it are dropped. This is the
    /// relevance threshold, and it is what <c>ApplicationConfiguration.RagThreshold</c> feeds — that
    /// property was previously persisted, validated, versioned and rolled back while nothing ever
    /// passed it here, so the control users saw in the UI had no effect on any query.
    /// </param>
    Task<List<VectorSearchResult>> HybridSearchAsync(string collectionName, float[] queryVector, string queryText, int limit = 10, float vectorWeight = 0.7f, float minRelevance = 0f, CancellationToken ct = default);

    Task DeleteVectorsAsync(string collectionName, List<Guid> pointIds, CancellationToken ct = default);

    /// <summary>
    /// Deletes every vector belonging to one chunk source — a KnowledgeBaseSource or a Document — in
    /// a single indexed SQL statement. This replaces a nearest-neighbour scan that only ever saw the
    /// top 100 matches, so any source with more chunks than that leaked the remainder on delete and
    /// again on every re-index.
    /// </summary>
    Task DeleteVectorsBySourceAsync(string collectionName, Guid sourceId, CancellationToken ct = default);
}
