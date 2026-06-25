namespace R2WAI.Domain.Common;

public abstract class BaseAuditableEntity<TId> : BaseEntity<TId>
{
    public string? CreatedBy { get; protected set; }
    public string? ModifiedBy { get; protected set; }

    public void SetCreatedBy(string createdBy)
    {
        CreatedBy = createdBy;
    }

    public void SetModifiedBy(string modifiedBy)
    {
        ModifiedBy = modifiedBy;
        MarkAsModified();
    }
}
