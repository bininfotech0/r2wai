using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Tests.Entities;

public class PromptTemplateTests
{
    [Fact]
    public void Constructor_SetsProperties_AndIsActiveByDefault()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var template = new PromptTemplate(id, tenantId, AssistantType.HR, "Custom HR prompt", version: 1);

        Assert.Equal(id, template.Id);
        Assert.Equal(tenantId, template.TenantId);
        Assert.Equal(AssistantType.HR, template.AssistantType);
        Assert.Equal("Custom HR prompt", template.Content);
        Assert.Equal(1, template.Version);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void Supersede_SetsIsActiveFalse()
    {
        var template = new PromptTemplate(Guid.NewGuid(), Guid.NewGuid(), AssistantType.IT, "content", version: 1);

        template.Supersede();

        Assert.False(template.IsActive);
    }
}
