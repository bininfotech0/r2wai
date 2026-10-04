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

    [Fact]
    public void UnlinkKnowledgeBase_ClearsKnowledgeBaseId()
    {
        // UpdateAssistantCommandHandler previously had no way to clear a KB once set — its
        // `if (KnowledgeBaseId.HasValue)` guard never fired for an explicit null. This is the
        // domain-level half of that fix (UnlinkKnowledgeBase flag on the command is the other).
        var assistant = CreateDefault();
        assistant.LinkKnowledgeBase(Guid.NewGuid());

        assistant.UnlinkKnowledgeBase();

        Assert.Null(assistant.KnowledgeBaseId);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Fact]
    public void UnlinkModelConfiguration_ClearsModelConfigId()
    {
        // Same bug, same fix shape, for the model selector: AssistantStudioPage's "Use tenant
        // default" option (value "") serialized to modelConfigurationId: undefined — omitted, not
        // cleared — so the control looked like it worked but silently didn't.
        var assistant = CreateDefault();
        assistant.LinkModelConfiguration(Guid.NewGuid());

        assistant.UnlinkModelConfiguration();

        Assert.Null(assistant.ModelConfigurationId);
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

    [Fact]
    public void GetEnabledToolIds_NeverConfigured_ReturnsNull()
    {
        var assistant = CreateDefault();
        Assert.Null(assistant.GetEnabledToolIds());
    }

    [Fact]
    public void DenyAllToolsByDefault_MakesGetEnabledToolIdsReturnEmptyNotNull()
    {
        // P0-4 (2026-09-20 audit): a brand-new assistant must start deny-by-default, not silently get
        // every tool via GetEnabledToolIds' null-means-all fallback (that fallback exists only to keep
        // already-created assistants working unchanged).
        var assistant = CreateDefault();

        assistant.DenyAllToolsByDefault();

        var result = assistant.GetEnabledToolIds();
        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.NotNull(assistant.ModifiedAt);
    }

    [Fact]
    public void GetEnabledToolIds_ExplicitlyEmptyArray_ReturnsEmptyNotNull()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Test", null, null, "[]", null);

        var result = assistant.GetEnabledToolIds();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetEnabledToolIds_ValidGuidArray_ReturnsParsedIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var assistant = CreateDefault();
        assistant.UpdateDetails("Test", null, null, $"[\"{id1}\",\"{id2}\"]", null);

        var result = assistant.GetEnabledToolIds();

        Assert.NotNull(result);
        Assert.Equal([id1, id2], result);
    }

    [Fact]
    public void GetEnabledToolIds_MalformedJson_ReturnsNull()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Test", null, null, "not valid json", null);

        Assert.Null(assistant.GetEnabledToolIds());
    }

    [Fact]
    public void GetBehaviorSettings_NeverConfigured_ReturnsNull()
    {
        var assistant = CreateDefault();
        Assert.Null(assistant.GetBehaviorSettings());
    }

    [Fact]
    public void GetBehaviorSettings_ValidJson_ParsesAllFields()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Test", null, null, null,
            "{\"responseStyle\":\"Concise\",\"answerLength\":\"Brief\",\"citationsEnabled\":false,\"askClarification\":true,\"temperature\":0.3,\"maxOutputTokens\":800}");

        var settings = assistant.GetBehaviorSettings();

        Assert.NotNull(settings);
        Assert.Equal("Concise", settings.ResponseStyle);
        Assert.Equal("Brief", settings.AnswerLength);
        Assert.False(settings.CitationsEnabled);
        Assert.True(settings.AskClarification);
        Assert.Equal(0.3, settings.Temperature);
        Assert.Equal(800, settings.MaxOutputTokens);
    }

    [Fact]
    public void GetBehaviorSettings_MalformedJson_ReturnsNull()
    {
        var assistant = CreateDefault();
        assistant.UpdateDetails("Test", null, null, null, "not valid json");

        Assert.Null(assistant.GetBehaviorSettings());
    }

    private static AssistantDefinition CreateDefault()
    {
        return new AssistantDefinition(Guid.NewGuid(), Guid.NewGuid(),
            "Test Assistant", AssistantType.General);
    }
}
