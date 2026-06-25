namespace R2WAI.Domain.Tests.Entities;

public class ConversationTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var conv = new Conversation(id, tenantId, userId, "Test Chat");

        Assert.Equal(id, conv.Id);
        Assert.Equal(tenantId, conv.TenantId);
        Assert.Equal(userId, conv.UserId);
        Assert.Equal("Test Chat", conv.Title);
        Assert.False(conv.IsArchived);
    }

    [Fact]
    public void AddMessage_AddsToMessages()
    {
        var conv = CreateDefault();
        conv.AddMessage(Guid.NewGuid(), null, MessageRole.User, "Hello");
        Assert.Single(conv.Messages);
    }

    [Fact]
    public void Archive_SetsIsArchived()
    {
        var conv = CreateDefault();
        conv.Archive();
        Assert.True(conv.IsArchived);
    }

    [Fact]
    public void Rename_ChangesTitle()
    {
        var conv = CreateDefault();
        conv.Rename("New Title");
        Assert.Equal("New Title", conv.Title);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var conv = CreateDefault();
        conv.SoftDelete();
        Assert.True(conv.IsDeleted);
    }

    private static Conversation CreateDefault()
    {
        return new Conversation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test");
    }
}
