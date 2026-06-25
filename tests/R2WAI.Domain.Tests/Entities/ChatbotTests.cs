namespace R2WAI.Domain.Tests.Entities;

public class ChatbotTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var kbId = Guid.NewGuid();
        var modelId = Guid.NewGuid();

        var chatbot = new Chatbot(id, tenantId, userId, "Test Bot", kbId, modelId);

        Assert.Equal(id, chatbot.Id);
        Assert.Equal(tenantId, chatbot.TenantId);
        Assert.Equal(userId, chatbot.UserId);
        Assert.Equal("Test Bot", chatbot.Name);
        Assert.Equal(kbId, chatbot.KnowledgeBaseId);
        Assert.Equal(modelId, chatbot.ModelConfigurationId);
        Assert.Equal(ChatbotStatus.Draft, chatbot.Status);
    }

    [Fact]
    public void UpdateDetails_ChangesProperties()
    {
        var chatbot = CreateDefaultChatbot();
        chatbot.UpdateDetails("New Name", "New desc", "Welcome!", "Q1, Q2", "System prompt");

        Assert.Equal("New Name", chatbot.Name);
        Assert.Equal("New desc", chatbot.Description);
        Assert.Equal("Welcome!", chatbot.WelcomeMessage);
        Assert.Equal("Q1, Q2", chatbot.SuggestedQuestions);
        Assert.Equal("System prompt", chatbot.PromptTemplate);
    }

    [Fact]
    public void UpdateStatus_ChangesStatus()
    {
        var chatbot = CreateDefaultChatbot();
        chatbot.UpdateStatus(ChatbotStatus.Active);
        Assert.Equal(ChatbotStatus.Active, chatbot.Status);
    }

    [Fact]
    public void UpdateWidget_SetsEmbedScriptAndSettings()
    {
        var chatbot = CreateDefaultChatbot();
        chatbot.UpdateWidget("<script>...</script>", "{\"color\":\"blue\"}");

        Assert.Equal("<script>...</script>", chatbot.EmbedScript);
        Assert.Equal("{\"color\":\"blue\"}", chatbot.WidgetSettings);
    }

    [Fact]
    public void LinkKnowledgeBase_SetsKnowledgeBaseId()
    {
        var chatbot = CreateDefaultChatbot();
        var kbId = Guid.NewGuid();
        chatbot.LinkKnowledgeBase(kbId);
        Assert.Equal(kbId, chatbot.KnowledgeBaseId);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var chatbot = CreateDefaultChatbot();
        chatbot.SoftDelete();
        Assert.True(chatbot.IsDeleted);
    }

    private static Chatbot CreateDefaultChatbot()
    {
        return new Chatbot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Bot", null, null);
    }
}
