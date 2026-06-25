using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class KnowledgeBase : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public KnowledgeBaseStatus Status { get; private set; } = KnowledgeBaseStatus.Creating;
    public string? EmbeddingModel { get; private set; }
    public int? ChunkSize { get; private set; }
    public int? ChunkOverlap { get; private set; }
    public int DocumentCount { get; private set; }
    public string? VectorCollectionName { get; private set; }
    public string? Metadata { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public ICollection<KnowledgeBaseSource> Sources { get; private set; } = [];
    public ICollection<Document> Documents { get; private set; } = [];

    private KnowledgeBase() { }

    public KnowledgeBase(Guid id, Guid tenantId, Guid userId, string name,
                          string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public void AddSource(KnowledgeBaseSource source)
    {
        Sources.Add(source);
        MarkAsModified();
    }

    public void RemoveSource(KnowledgeBaseSource source)
    {
        Sources.Remove(source);
        MarkAsModified();
    }

    public void IncrementDocumentCount()
    {
        DocumentCount++;
        MarkAsModified();
    }

    public void UpdateStatus(KnowledgeBaseStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    public void ConfigureEmbedding(string model, int chunkSize, int chunkOverlap, string vectorCollectionName)
    {
        EmbeddingModel = model;
        ChunkSize = chunkSize;
        ChunkOverlap = chunkOverlap;
        VectorCollectionName = vectorCollectionName;
        MarkAsModified();
    }

    public void SetMetadata(string metadata)
    {
        Metadata = metadata;
        MarkAsModified();
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        MarkAsModified();
    }
}
