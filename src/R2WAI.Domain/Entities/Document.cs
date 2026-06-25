using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Document : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? KnowledgeBaseId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DocumentType FileType { get; private set; }
    public string FilePath { get; private set; }
    public long FileSize { get; private set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Uploading;
    public string? ProcessingError { get; private set; }
    public int? PageCount { get; private set; }
    public string? VectorIds { get; private set; }
    public string? Metadata { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public KnowledgeBase? KnowledgeBase { get; private set; }

    private Document() { }

    public Document(Guid id, Guid tenantId, Guid userId, string name,
                    DocumentType fileType, string filePath, long fileSize,
                    Guid? knowledgeBaseId = null, string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        FileType = fileType;
        FilePath = filePath;
        FileSize = fileSize;
        KnowledgeBaseId = knowledgeBaseId;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(DocumentStatus status, string? error = null)
    {
        Status = status;
        if (error is not null)
            ProcessingError = error;
        MarkAsModified();
    }

    public void SetPageCount(int pageCount)
    {
        PageCount = pageCount;
        MarkAsModified();
    }

    public void SetVectorIds(string vectorIds)
    {
        VectorIds = vectorIds;
        MarkAsModified();
    }

    public void SetMetadata(string metadata)
    {
        Metadata = metadata;
        MarkAsModified();
    }
}
