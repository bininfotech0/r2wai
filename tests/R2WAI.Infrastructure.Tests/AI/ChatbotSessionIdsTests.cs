namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers the pure helpers behind chatbot session memory: which caller-supplied session ids are
/// accepted, and how session history and knowledge-base excerpts share the AI's one context slot.
/// </summary>
public class ChatbotSessionIdsTests
{
    [Theory]
    [InlineData("3f2b8c1e-9a4d-4e7b-8c2a-1d5e6f7a8b9c")] // widget's crypto.randomUUID()
    [InlineData("whatsapp:+919876543210")]               // webhook caller's own thread key
    [InlineData("user@example.com")]
    [InlineData("thread_42.v2")]
    public void Normalize_AcceptsUuidsAndCommonThreadKeys(string sessionId)
    {
        Assert.Equal(sessionId, ChatbotSessionIds.Normalize(sessionId));
    }

    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        Assert.Equal("abc-123", ChatbotSessionIds.Normalize("  abc-123 \n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("quote'injection")]
    [InlineData("new\nline")]
    [InlineData("emoji😀")]
    public void Normalize_RejectsMissingOrUnsafeIds_SoTheRequestRunsWithoutMemory(string? sessionId)
    {
        Assert.Null(ChatbotSessionIds.Normalize(sessionId));
    }

    [Fact]
    public void Normalize_EnforcesTheColumnLength()
    {
        Assert.NotNull(ChatbotSessionIds.Normalize(new string('a', ChatbotSessionIds.MaxLength)));
        Assert.Null(ChatbotSessionIds.Normalize(new string('a', ChatbotSessionIds.MaxLength + 1)));
    }

    [Fact]
    public void Scope_PrefixesTheChannel_SoTheSameCallerIdNeverCollidesAcrossChannels()
    {
        var widget = ChatbotSessionIds.Scope(ChatbotSessionIds.WidgetChannel, "whatsapp:+919876543210");
        var webhook = ChatbotSessionIds.Scope(ChatbotSessionIds.WebhookChannel, "whatsapp:+919876543210");

        Assert.Equal("widget:whatsapp:+919876543210", widget);
        Assert.Equal("webhook:whatsapp:+919876543210", webhook);
        Assert.NotEqual(widget, webhook);
    }

    [Fact]
    public void Scope_UnusableId_ReturnsNull()
    {
        Assert.Null(ChatbotSessionIds.Scope(ChatbotSessionIds.WebhookChannel, "has space"));
        Assert.Null(ChatbotSessionIds.Scope(ChatbotSessionIds.WidgetChannel, null));
    }

    [Fact]
    public void Scope_LongestAcceptedId_StillFitsTheColumn()
    {
        var scoped = ChatbotSessionIds.Scope(ChatbotSessionIds.WebhookChannel, new string('a', ChatbotSessionIds.MaxLength));
        Assert.True(scoped!.Length <= 100, $"Stored key is {scoped.Length} chars; the SessionId column is 100.");
    }

    [Fact]
    public void ComposeContext_NeitherPart_ReturnsNull()
    {
        Assert.Null(ChatbotSessionIds.ComposeContext(null, null));
        Assert.Null(ChatbotSessionIds.ComposeContext("  ", ""));
    }

    [Fact]
    public void ComposeContext_KnowledgeOnly_IsPassedThroughUnchanged()
    {
        // Byte-identical to the pre-memory behavior for a request without a session id.
        Assert.Equal("Refunds take 5 days.", ChatbotSessionIds.ComposeContext(null, "Refunds take 5 days."));
    }

    [Fact]
    public void ComposeContext_HistoryOnly_IsLabelled()
    {
        Assert.Equal("[Conversation so far]\nUser: hi\nAssistant: hello",
            ChatbotSessionIds.ComposeContext("User: hi\nAssistant: hello", null));
    }

    [Fact]
    public void ComposeContext_Both_PutsHistoryFirst_SoTruncationDropsOldTurnsBeforeKnowledge()
    {
        var result = ChatbotSessionIds.ComposeContext("User: hi", "Refunds take 5 days.");

        Assert.Equal("[Conversation so far]\nUser: hi\n\n[Relevant knowledge]\nRefunds take 5 days.", result);
        Assert.True(result!.IndexOf("User: hi", StringComparison.Ordinal)
            < result.IndexOf("Refunds", StringComparison.Ordinal));
    }
}
