using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers ConversationContextBuilder — the pure split/format logic behind Context &amp; Memory's
/// short-term/long-term split. No DB or AI-service involved; ConversationMemoryService (the thin I/O
/// wrapper around this) is exercised indirectly via ChatService once the "AI:ContextMemory:
/// SummarizationEnabled" flag is enabled in an environment with a real database — out of scope here,
/// since the flag defaults off and this is the algorithm the flag gates.
/// </summary>
public class ConversationContextBuilderTests
{
    private static (MessageRole, string) U(string content) => (MessageRole.User, content);
    private static (MessageRole, string) A(string content) => (MessageRole.Assistant, content);

    [Fact]
    public void JoinTurns_FormatsUserAndAssistantRoles()
    {
        var turns = new[] { U("hello"), A("hi there") };
        var result = ConversationContextBuilder.JoinTurns(turns);
        Assert.Equal("User: hello\nAssistant: hi there", result);
    }

    [Fact]
    public void JoinTurns_NonUserRole_RendersAsAssistant()
    {
        // System-role messages (if any ever appear in this window) render as "Assistant" rather
        // than leaking the raw enum name — matches ChatService's original ternary exactly.
        var turns = new[] { (MessageRole.System, "internal note") };
        var result = ConversationContextBuilder.JoinTurns(turns);
        Assert.Equal("Assistant: internal note", result);
    }

    [Fact]
    public void Split_FewerMessagesThanRecentCount_NoOlderText()
    {
        var turns = new[] { U("a"), A("b") };
        var (older, recent) = ConversationContextBuilder.Split(turns, recentTurnCount: 10);

        Assert.Null(older);
        Assert.Equal("User: a\nAssistant: b", recent);
    }

    [Fact]
    public void Split_ExactlyRecentCount_NoOlderText()
    {
        var turns = new[] { U("a"), A("b") };
        var (older, recent) = ConversationContextBuilder.Split(turns, recentTurnCount: 2);

        Assert.Null(older);
        Assert.Equal("User: a\nAssistant: b", recent);
    }

    [Fact]
    public void Split_MoreMessagesThanRecentCount_SplitsIntoOlderAndRecent()
    {
        var turns = new[] { U("1"), A("2"), U("3"), A("4"), U("5") };
        var (older, recent) = ConversationContextBuilder.Split(turns, recentTurnCount: 2);

        Assert.Equal("User: 1\nAssistant: 2\nUser: 3", older);
        Assert.Equal("Assistant: 4\nUser: 5", recent);
    }

    [Fact]
    public void ComposeWithSummary_NullSummary_ReturnsRecentTextOnly()
    {
        var result = ConversationContextBuilder.ComposeWithSummary(null, "recent text");
        Assert.Equal("recent text", result);
    }

    [Fact]
    public void ComposeWithSummary_EmptySummary_ReturnsRecentTextOnly()
    {
        var result = ConversationContextBuilder.ComposeWithSummary("   ", "recent text");
        Assert.Equal("recent text", result);
    }

    [Fact]
    public void ComposeWithSummary_ValidSummary_PrependsLabelledSummary()
    {
        var result = ConversationContextBuilder.ComposeWithSummary("user asked about X", "recent text");
        Assert.Equal("[Earlier conversation summary]\nuser asked about X\n\n[Recent messages]\nrecent text", result);
    }
}
