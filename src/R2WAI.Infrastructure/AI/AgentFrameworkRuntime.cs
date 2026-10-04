using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.AI.ModelGateway;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Microsoft Agent Framework backing for <see cref="IAgentRuntime"/> (implementation plan Phase 4)
/// — proves the seam is swappable, alongside the existing Semantic-Kernel-backed
/// <see cref="AgentRuntime"/>; selection between the two is <see cref="AgentRuntimeSelector"/>'s job,
/// not this class's.
///
/// Deliberately narrow scope for this first cut:
/// - OpenAI provider only. Mirrors <c>OpenAiModelProvider</c>'s own client construction and config
///   keys (<c>AI:OpenAI:ApiKey</c>/<c>ModelId</c>/<c>Endpoint</c>) so the two runtimes are fed
///   identical credentials/model — there is no tenant-specific model resolution here, matching
///   <see cref="AgentRuntime"/>'s own "process-wide default" scope (it never receives a
///   <c>ResolvedModelConfig</c> either).
/// - Read-only tools only — see <see cref="MafToolFunctionFactory"/>.
/// - No mid-history truncation (unlike <c>SemanticKernelService.ChatAsync</c>'s
///   <c>TruncateInputText</c>) — a real gap for a long conversation history, not exercised by the
///   short prompts a parity test or a first read-only flow actually sends; flagged rather than
///   silently ignored.
/// </summary>
public class AgentFrameworkRuntime : IAgentRuntime
{
    private readonly IConfiguration _configuration;
    private readonly MafToolFunctionFactory _toolFactory;
    private readonly IChatTraceCollector _traceCollector;

    public AgentFrameworkRuntime(IConfiguration configuration, MafToolFunctionFactory toolFactory, IChatTraceCollector traceCollector)
    {
        _configuration = configuration;
        _toolFactory = toolFactory;
        _traceCollector = traceCollector;
    }

    public async Task<AgentRuntimeResult> InvokeAsync(AgentRuntimeRequest request, CancellationToken ct = default)
    {
        _traceCollector.Clear();

        var agent = await BuildAgentAsync(request.SystemPrompt, request.EnabledToolIds, ct);
        var response = await agent.RunAsync(BuildMessages(request.Message, request.ConversationHistory), cancellationToken: ct);

        var toolCallsMade = _traceCollector.GetTrace().Select(t => t.Function).ToList();
        return new AgentRuntimeResult(response.Text, request.CorrelationId ?? Guid.NewGuid(), toolCallsMade);
    }

    public async IAsyncEnumerable<string> StreamAsync(AgentRuntimeRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var agent = await BuildAgentAsync(request.SystemPrompt, request.EnabledToolIds, ct);
        await foreach (var update in agent.RunStreamingAsync(BuildMessages(request.Message, request.ConversationHistory), cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    private async Task<AIAgent> BuildAgentAsync(string? systemPrompt, IReadOnlyCollection<Guid>? enabledToolIds, CancellationToken ct)
    {
        var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "The Agent Framework runtime requires AI:OpenAI:ApiKey to be configured — this first cut supports the OpenAI provider only.");

        var modelId = _configuration["AI:OpenAI:ModelId"] ?? "gpt-4o";
        var endpoint = _configuration["AI:OpenAI:Endpoint"];

        var clientOptions = new OpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(3), NetworkTimeout = ModelGatewayDefaults.CloudNetworkTimeout };
        if (!string.IsNullOrEmpty(endpoint))
            clientOptions.Endpoint = new Uri(endpoint);

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        var chatClient = client.GetChatClient(modelId);

        var tools = await _toolFactory.BuildToolsAsync(enabledToolIds, ct);
        var instructions = string.IsNullOrEmpty(systemPrompt) ? DefaultSystemPrompt : systemPrompt;
        return chatClient.AsAIAgent(instructions: instructions, tools: tools.ToList());
    }

    // Matches SemanticKernelService.ChatAsync's own default system message verbatim, for parity.
    private const string DefaultSystemPrompt =
        "You are R2WAI, an intelligent enterprise AI assistant specialized in work execution, approvals, and document intelligence.";

    // Mirrors SemanticKernelService.ChatAsync's own message shape (system prompt, then conversation
    // history as a prior user message, then the real message) so a parity comparison sees the model
    // given the same structure — see this class's own doc comment for the one known divergence
    // (no truncation of a long history here).
    private static List<ChatMessage> BuildMessages(string message, string? conversationHistory)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(conversationHistory))
            messages.Add(new ChatMessage(ChatRole.User, conversationHistory));
        messages.Add(new ChatMessage(ChatRole.User, message));
        return messages;
    }
}
