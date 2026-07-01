namespace R2WAI.Application.Features.Chatbots.DTOs;

public class ChatbotDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? SuggestedQuestions { get; init; }
    public Guid? ModelConfigurationId { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public string? PromptTemplate { get; init; }
    public bool VoiceEnabled { get; init; }
    public ChatbotStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
