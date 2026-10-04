namespace R2WAI.Domain.Tests.Entities;

public class AssistantPromptHistoryTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var history = new AssistantPromptHistory(id, tenantId, assistantId, "You are a helpful assistant.", 1);

        Assert.Equal(id, history.Id);
        Assert.Equal(tenantId, history.TenantId);
        Assert.Equal(assistantId, history.AssistantDefinitionId);
        Assert.Equal("You are a helpful assistant.", history.Content);
        Assert.Equal(1, history.Version);
        Assert.True(history.IsActive);
    }

    [Fact]
    public void Supersede_ClearsActiveFlag()
    {
        var history = new AssistantPromptHistory(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "prompt", 1);

        history.Supersede();

        Assert.False(history.IsActive);
    }
}
