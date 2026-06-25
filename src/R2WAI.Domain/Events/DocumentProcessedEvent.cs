using R2WAI.Domain.Common;

namespace R2WAI.Domain.Events;

public sealed class DocumentProcessedEvent : BaseDomainEvent
{
    public Guid DocumentId { get; }
    public Guid TenantId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public int? PageCount { get; }

    public DocumentProcessedEvent(Guid documentId, Guid tenantId, bool success,
                                   string? error = null, int? pageCount = null)
    {
        DocumentId = documentId;
        TenantId = tenantId;
        Success = success;
        Error = error;
        PageCount = pageCount;
    }
}
