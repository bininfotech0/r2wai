using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Events;

public sealed class DocumentUploadedEvent : BaseDomainEvent
{
    public Guid DocumentId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string FileName { get; }
    public DocumentType FileType { get; }
    public long FileSize { get; }
    public Guid? KnowledgeBaseId { get; }

    public DocumentUploadedEvent(Guid documentId, Guid tenantId, Guid userId,
                                  string fileName, DocumentType fileType,
                                  long fileSize, Guid? knowledgeBaseId = null)
    {
        DocumentId = documentId;
        TenantId = tenantId;
        UserId = userId;
        FileName = fileName;
        FileType = fileType;
        FileSize = fileSize;
        KnowledgeBaseId = knowledgeBaseId;
    }
}
