using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class KnowledgeBaseSource : BaseEntity<Guid>
{
    public Guid KnowledgeBaseId { get; private set; }
    public string Type { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Url { get; private set; }
    public string? Content { get; private set; }
    public string? Status { get; private set; }
    public int ChunkCount { get; private set; }
    public DateTime? IndexedAt { get; private set; }
    public string? Error { get; private set; }

    public KnowledgeBase KnowledgeBase { get; private set; } = null!;

    private KnowledgeBaseSource() { }

    public KnowledgeBaseSource(Guid id, Guid knowledgeBaseId, string type,
                                Guid? referenceId = null, string? url = null,
                                string? content = null)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        Type = type;
        ReferenceId = referenceId;
        Url = url;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
        MarkAsModified();
    }

    public void MarkIndexed(int chunkCount)
    {
        Status = "Indexed";
        ChunkCount = chunkCount;
        IndexedAt = DateTime.UtcNow;
        Error = null;
        MarkAsModified();
    }

    public void MarkFailed(string error)
    {
        Status = "Failed";
        Error = error;
        MarkAsModified();
    }

    public void MarkProcessing()
    {
        Status = "Processing";
        Error = null;
        MarkAsModified();
    }
}
