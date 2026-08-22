using System.Runtime.CompilerServices;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

public class AgentRuntime : IAgentRuntime
{
    private readonly IAIService _aiService;

    public AgentRuntime(IAIService aiService)
    {
        _aiService = aiService;
    }

    public Task<string> InvokeAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default) =>
        _aiService.ChatAsync(message, conversationHistory, systemPrompt, enableTools: true, ct);

    public async IAsyncEnumerable<string> StreamAsync(string message, string? conversationHistory = null, string? systemPrompt = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in _aiService.StreamChatAsync(message, conversationHistory, systemPrompt, enableTools: true, ct: ct))
            yield return chunk;
    }
}
