using R2WAI.Domain.Common;

namespace R2WAI.Domain.Events;

public sealed class DocumentProcessedEvent : BaseDomainEvent
{
    public Guid DocumentId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string Name { get; }
    public bool Success { get; }
    public string? Error { get; }
    public int? PageCount { get; }

    public DocumentProcessedEvent(Guid documentId, Guid tenantId, Guid userId, string name, bool success,
                                   string? error = null, int? pageCount = null)
    {
        DocumentId = documentId;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Success = success;
        Error = error;
        PageCount = pageCount;
    }
}
