namespace R2WAI.Application.Features.Members.DTOs;

public class PointsTransactionDto
{
    public Guid Id { get; init; }
    public int Points { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? RelatedEventId { get; init; }
    public DateTime CreatedAt { get; init; }
}
