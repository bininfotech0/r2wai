namespace R2WAI.Application.Features.Chat.DTOs;

public class SuggestedActionDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
}
