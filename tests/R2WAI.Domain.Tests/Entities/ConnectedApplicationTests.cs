namespace R2WAI.Domain.Tests.Entities;

public class ConnectedApplicationTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        var application = new ConnectedApplication(
            id, tenantId, departmentId, "Property Tax", "PROP",
            "Property tax assessments", "https://tax.example.gov");

        Assert.Equal(id, application.Id);
        Assert.Equal(tenantId, application.TenantId);
        Assert.Equal(departmentId, application.DepartmentId);
        Assert.Equal("Property Tax", application.Name);
        Assert.Equal("PROP", application.Code);
        Assert.Equal("https://tax.example.gov", application.BaseUrl);
        Assert.Equal(ApplicationEnvironment.Development, application.Environment);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
        Assert.Null(application.PublishedAt);
    }

    [Fact]
    public void Create_WithoutDepartment_LeavesDepartmentIdNull()
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), null, "Supplier Portal", "SUP-PORTAL",
            "Supplier-facing services.");

        Assert.Null(application.DepartmentId);
        Assert.Null(application.Department);
        Assert.Equal("Supplier Portal", application.Name);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
    }

    [Fact]
    public void UpdateDetails_ChangesFields()
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Property Tax", "PROP");

        application.UpdateDetails(
            "Property Tax Portal", "Updated", "https://tax.example.gov",
            ApplicationEnvironment.Production);

        Assert.Equal("Property Tax Portal", application.Name);
        Assert.Equal("Updated", application.Description);
        Assert.Equal(ApplicationEnvironment.Production, application.Environment);
        Assert.NotNull(application.ModifiedAt);
    }

    [Fact]
    public void Publish_SetsPublishedStatusAndTimestamp()
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Property Tax", "PROP");

        application.Publish();

        Assert.Equal(ApplicationStatus.Published, application.Status);
        Assert.NotNull(application.PublishedAt);
    }

    [Theory]
    [InlineData(ApplicationAction.StartDiscovery, ApplicationStatus.Discovering)]
    [InlineData(ApplicationAction.MarkConfiguring, ApplicationStatus.Configuring)]
    [InlineData(ApplicationAction.MarkTesting, ApplicationStatus.Testing)]
    [InlineData(ApplicationAction.Publish, ApplicationStatus.Published)]
    [InlineData(ApplicationAction.Disable, ApplicationStatus.Disabled)]
    [InlineData(ApplicationAction.Archive, ApplicationStatus.Archived)]
    public void Lifecycle_Transitions_ToExpectedStatus(ApplicationAction action, ApplicationStatus expected)
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Property Tax", "PROP");

        ApplyAction(application, action);

        Assert.Equal(expected, application.Status);
    }

    [Fact]
    public void Enable_RestoresPublishedStatus()
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Property Tax", "PROP");
        application.Publish();
        application.Disable();

        application.Enable();

        Assert.Equal(ApplicationStatus.Published, application.Status);
    }

    [Fact]
    public void Archived_Application_ThrowsOnModification()
    {
        var application = new ConnectedApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Property Tax", "PROP");
        application.Archive();

        Assert.Throws<InvalidOperationException>(() => application.UpdateDetails(
            "X", null, null, ApplicationEnvironment.Production));
        Assert.Throws<InvalidOperationException>(() => application.Publish());
        Assert.Throws<InvalidOperationException>(() => application.StartDiscovery());
    }

    private static void ApplyAction(ConnectedApplication application, ApplicationAction action)
    {
        switch (action)
        {
            case ApplicationAction.StartDiscovery:
                application.StartDiscovery();
                break;
            case ApplicationAction.MarkConfiguring:
                application.MarkConfiguring();
                break;
            case ApplicationAction.MarkTesting:
                application.MarkTesting();
                break;
            case ApplicationAction.Publish:
                application.Publish();
                break;
            case ApplicationAction.Disable:
                application.Disable();
                break;
            case ApplicationAction.Enable:
                application.Enable();
                break;
            case ApplicationAction.Archive:
                application.Archive();
                break;
        }
    }
}
