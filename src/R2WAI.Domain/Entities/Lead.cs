using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Lead : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? ChatbotId { get; private set; }
    public string Name { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? ClassOrGrade { get; private set; }
    public string? Interest { get; private set; }
    public string? Source { get; private set; }
    public LeadStatus Status { get; private set; } = LeadStatus.New;
    public string? Notes { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public Chatbot? Chatbot { get; private set; }

    private Lead() { }

    public Lead(Guid id, Guid tenantId, string name, Guid? chatbotId = null,
                string? phone = null, string? email = null, string? classOrGrade = null,
                string? interest = null, string? source = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        ChatbotId = chatbotId;
        Phone = phone;
        Email = email;
        ClassOrGrade = classOrGrade;
        Interest = interest;
        Source = source;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? phone, string? email,
                               string? classOrGrade, string? interest)
    {
        Name = name;
        Phone = phone;
        Email = email;
        ClassOrGrade = classOrGrade;
        Interest = interest;
        MarkAsModified();
    }

    public void UpdateStatus(LeadStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        MarkAsModified();
    }
}
