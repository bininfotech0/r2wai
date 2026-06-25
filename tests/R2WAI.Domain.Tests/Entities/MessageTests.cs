namespace R2WAI.Domain.Tests.Entities;

public class MessageTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var message = new Message(id, conversationId, tenantId, userId,
            null, MessageRole.User, "Hello, help me with this document.");

        Assert.Equal(id, message.Id);
        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(tenantId, message.TenantId);
        Assert.Equal(userId, message.UserId);
        Assert.Null(message.ParentMessageId);
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Hello, help me with this document.", message.Content);
        Assert.Equal(MessageStatus.Sending, message.Status);
    }

    [Fact]
    public void Create_WithAllOptionalFields_SetsCorrectly()
    {
        var parentId = Guid.NewGuid();
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), parentId, MessageRole.Assistant,
            "Here is your answer.", "[{\"type\":\"text\"}]", "gpt-4o", 150, "{\"citations\":3}");

        Assert.Equal(parentId, message.ParentMessageId);
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal("[{\"type\":\"text\"}]", message.ContentBlocks);
        Assert.Equal("gpt-4o", message.ModelUsed);
        Assert.Equal(150, message.TokensUsed);
        Assert.Equal("{\"citations\":3}", message.Metadata);
    }

    [Fact]
    public void UpdateStatus_ChangesStatus()
    {
        var message = CreateDefault();
        Assert.Equal(MessageStatus.Sending, message.Status);

        message.UpdateStatus(MessageStatus.Sent);

        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.NotNull(message.ModifiedAt);
    }

    [Fact]
    public void SetTokensUsed_UpdatesTokenCount()
    {
        var message = CreateDefault();
        Assert.Null(message.TokensUsed);

        message.SetTokensUsed(350);

        Assert.Equal(350, message.TokensUsed);
        Assert.NotNull(message.ModifiedAt);
    }

    [Theory]
    [InlineData(MessageRole.User)]
    [InlineData(MessageRole.Assistant)]
    [InlineData(MessageRole.System)]
    public void Create_WithDifferentRoles_Works(MessageRole role)
    {
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, role, "Test message");

        Assert.Equal(role, message.Role);
    }

    private static Message CreateDefault()
    {
        return new Message(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, MessageRole.User, "Test message");
    }
}
