namespace R2WAI.Application.Features.Chat.DTOs;

public class ConversationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Module { get; init; }
    public int MessageCount { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
