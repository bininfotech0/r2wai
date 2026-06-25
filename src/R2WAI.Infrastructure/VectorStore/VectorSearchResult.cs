namespace R2WAI.Infrastructure.VectorStore;

public class VectorSearchResult
{
    public Guid Id { get; set; }
    public float Score { get; set; }
    public Dictionary<string, object?>? Payload { get; set; }
}
