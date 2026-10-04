namespace R2WAI.Application.Features.Chat.DTOs;

public class ConversationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Module { get; init; }
    // Backfilled from Conversation.ReferenceId only when Module == "assistant" — that's the one
    // convention ChatWithAssistantCommandHandler actually follows when creating a conversation
    // (ReferenceId = assistant.Id); ReferenceId itself isn't a typed FK, so this is real but
    // convention-based rather than a guaranteed relationship for every module.
    public string? AssistantName { get; set; }
    public string? UserName { get; set; }
    public int MessageCount { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
