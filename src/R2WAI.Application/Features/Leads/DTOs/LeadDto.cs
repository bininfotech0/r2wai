namespace R2WAI.Application.Features.Leads.DTOs;

public class LeadDto
{
    public Guid Id { get; init; }
    public Guid? ChatbotId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ClassOrGrade { get; init; }
    public string? Interest { get; init; }
    public string? Source { get; init; }
    public LeadStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}
