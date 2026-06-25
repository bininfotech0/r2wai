namespace R2WAI.Infrastructure.VectorStore;

public interface IVectorStoreService
{
    Task CreateCollectionAsync(string collectionName, int vectorSize, CancellationToken ct = default);
    Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default);
    Task UpsertVectorsAsync(string collectionName, List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)> points, CancellationToken ct = default);
    Task<List<VectorSearchResult>> SearchVectorsAsync(string collectionName, float[] queryVector, int limit = 10, CancellationToken ct = default);
    Task<List<VectorSearchResult>> HybridSearchAsync(string collectionName, float[] queryVector, string queryText, int limit = 10, float vectorWeight = 0.7f, CancellationToken ct = default);
    Task DeleteVectorsAsync(string collectionName, List<Guid> pointIds, CancellationToken ct = default);
}
