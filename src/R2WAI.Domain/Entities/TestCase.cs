using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class TestCase : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public string? ExpectedResponseContains { get; private set; }
    public string? ExpectedCapabilityCalled { get; private set; }
    public bool IsEnabled { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public AssistantDefinition Assistant { get; private set; } = null!;
    public ConnectedApplication? Application { get; private set; }

    private TestCase() { }

    public TestCase(Guid id, Guid tenantId, Guid assistantId, string name, string question,
        Guid? applicationId = null, string? expectedResponseContains = null, string? expectedCapabilityCalled = null)
    {
        Id = id;
        TenantId = tenantId;
        AssistantId = assistantId;
        ApplicationId = applicationId;
        Name = name;
        Question = question;
        ExpectedResponseContains = expectedResponseContains;
        ExpectedCapabilityCalled = expectedCapabilityCalled;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string question, string? expectedResponseContains, string? expectedCapabilityCalled)
    {
        Name = name;
        Question = question;
        ExpectedResponseContains = expectedResponseContains;
        ExpectedCapabilityCalled = expectedCapabilityCalled;
        MarkAsModified();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        MarkAsModified();
    }
}
