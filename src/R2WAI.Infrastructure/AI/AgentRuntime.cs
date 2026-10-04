using System.Runtime.CompilerServices;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

public class AgentRuntime : IAgentRuntime
{
    private readonly IAIService _aiService;
    private readonly IChatTraceCollector _traceCollector;

    public AgentRuntime(IAIService aiService, IChatTraceCollector traceCollector)
    {
        _aiService = aiService;
        _traceCollector = traceCollector;
    }

    public async Task<AgentRuntimeResult> InvokeAsync(AgentRuntimeRequest request, CancellationToken ct = default)
    {
        // Clear-before/read-after rather than assuming a fresh scope: IChatTraceCollector is
        // request-scoped, but nothing stops two IAgentRuntime calls from sharing one HTTP request —
        // this keeps ToolCallsMade this call's own, not an accumulation of an earlier one.
        _traceCollector.Clear();

        var text = await _aiService.ChatAsync(
            request.Message, request.ConversationHistory, request.SystemPrompt,
            enableTools: true, modelConfig: null, request.EnabledToolIds, ct);

        var toolCallsMade = _traceCollector.GetTrace().Select(t => t.Function).ToList();
        return new AgentRuntimeResult(text, request.CorrelationId ?? Guid.NewGuid(), toolCallsMade);
    }

    public async IAsyncEnumerable<string> StreamAsync(AgentRuntimeRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in _aiService.StreamChatAsync(
            request.Message, request.ConversationHistory, request.SystemPrompt,
            enableTools: true, modelConfig: null, request.EnabledToolIds, ct))
            yield return chunk;
    }
}
