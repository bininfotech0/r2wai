namespace R2WAI.Domain.Tests.Entities;

public class AssistantDefinitionTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var kbId = Guid.NewGuid();

        var assistant = new AssistantDefinition(id, tenantId, "HR Assistant",
            AssistantType.HR, modelId, kbId);

        Assert.Equal(id, assistant.Id);
        Assert.Equal(tenantId, assistant.TenantId);
        Assert.Equal("HR Assistant", assistant.Name);
        Assert.Equal(AssistantType.HR, assistant.Type);
        Assert.Equal(modelId, assistant.ModelConfigurationId);
        Assert.Equal(kbId, assistant.KnowledgeBaseId);
        Assert.False(assistant.IsActive);
        Assert.Null(assistant.Description);
        Assert.Null(assistant.SystemPrompt);
    }

    [Fact]
    public void Create_WithMinimalData_DefaultsToInactive()
    {
        var assistant = new AssistantDefinition(Guid.NewGuid(), Guid.NewGuid(),
            "Test", AssistantType.General);

        Assert.False(assistant.IsActive);
        Assert.Null(assistant.ModelConfigurationId);
        Assert.Null(assistant.KnowledgeBaseId);
    }

    [Fact]
    public void UpdateDetails_ChangesAllFields()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Updated Name", "A description", "You are a helpful assistant.",
            "[\"search\", \"email\"]", "{\"temperature\": 0.7}");

        Assert.Equal("Updated Name", assistant.Name);
        Assert.Equal("A description", assistant.Description);
        Assert.Equal("You are a helpful assistant.", assistant.SystemPrompt);
        Assert.Equal("[\"search\", \"email\"]", assistant.Tools);
        Assert.Equal("{\"temperature\": 0.7}", assistant.Settings);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Fact]
    public void UpdateDetails_WithNulls_ClearsOptionalFields()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Updated Name", "desc", "prompt", "tools", "settings");
        assistant.UpdateDetails("Name Only", null, null, null, null);

        Assert.Equal("Name Only", assistant.Name);
        Assert.Null(assistant.Description);
        Assert.Null(assistant.SystemPrompt);
        Assert.Null(assistant.Tools);
        Assert.Null(assistant.Settings);
    }

    [Fact]
    public void Publish_SetsIsActive()
    {
        var assistant = CreateDefault();
        Assert.False(assistant.IsActive);

        assistant.Publish();

        Assert.True(assistant.IsActive);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Fact]
    public void Unpublish_ClearsIsActive()
    {
        var assistant = CreateDefault();
        assistant.Publish();
        Assert.True(assistant.IsActive);

        assistant.Unpublish();

        Assert.False(assistant.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActive()
    {
        var assistant = CreateDefault();
        assistant.Activate();
        Assert.True(assistant.IsActive);
    }

    [Fact]
    public void Deactivate_ClearsIsActive()
    {
        var assistant = CreateDefault();
        assistant.Activate();
        assistant.Deactivate();
        Assert.False(assistant.IsActive);
    }

    [Fact]
    public void LinkModelConfiguration_SetsModelConfigId()
    {
        var assistant = CreateDefault();
        var modelId = Guid.NewGuid();

        assistant.LinkModelConfiguration(modelId);

        Assert.Equal(modelId, assistant.ModelConfigurationId);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Fact]
    public void LinkKnowledgeBase_SetsKnowledgeBaseId()
    {
        var assistant = CreateDefault();
        var kbId = Guid.NewGuid();

        assistant.LinkKnowledgeBase(kbId);

        Assert.Equal(kbId, assistant.KnowledgeBaseId);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Theory]
    [InlineData(AssistantType.General)]
    [InlineData(AssistantType.HR)]
    [InlineData(AssistantType.IT)]
    [InlineData(AssistantType.Finance)]
    [InlineData(AssistantType.Legal)]
    [InlineData(AssistantType.Procurement)]
    public void Create_WithAllAssistantTypes_Succeeds(AssistantType type)
    {
        var assistant = new AssistantDefinition(Guid.NewGuid(), Guid.NewGuid(), "Test", type);
        Assert.Equal(type, assistant.Type);
    }

    [Fact]
    public void PublishUnpublishCycle_TogglesCorrectly()
    {
        var assistant = CreateDefault();

        assistant.Publish();
        Assert.True(assistant.IsActive);

        assistant.Unpublish();
        Assert.False(assistant.IsActive);

        assistant.Publish();
        Assert.True(assistant.IsActive);
    }

    private static AssistantDefinition CreateDefault()
    {
        return new AssistantDefinition(Guid.NewGuid(), Guid.NewGuid(),
            "Test Assistant", AssistantType.General);
    }
}
