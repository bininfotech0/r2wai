namespace R2WAI.Domain.Tests.Entities;

public class WorkflowTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var workflow = new Workflow(id, tenantId, userId, "My Workflow",
            "Description", "sequential", "[{\"name\":\"step1\"}]");

        Assert.Equal(id, workflow.Id);
        Assert.Equal("My Workflow", workflow.Name);
        Assert.Equal("Description", workflow.Description);
        Assert.Equal("sequential", workflow.Type);
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActive()
    {
        var workflow = CreateDefault();
        workflow.Activate();
        Assert.True(workflow.IsActive);
    }

    [Fact]
    public void Pause_ClearsIsActive()
    {
        var workflow = CreateDefault();
        workflow.Activate();
        workflow.Pause();
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public void UpdateDetails_ChangesAllFields()
    {
        var workflow = CreateDefault();
        workflow.UpdateDetails("Updated", "New desc", "parallel", "cron", "[{\"name\":\"s1\"}]");

        Assert.Equal("Updated", workflow.Name);
        Assert.Equal("New desc", workflow.Description);
        Assert.Equal("parallel", workflow.Type);
        Assert.Equal("cron", workflow.Trigger);
        Assert.Contains("s1", workflow.Steps);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var workflow = CreateDefault();
        workflow.SoftDelete();
        Assert.True(workflow.IsDeleted);
    }

    private static Workflow CreateDefault()
    {
        return new Workflow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Workflow");
    }
}
