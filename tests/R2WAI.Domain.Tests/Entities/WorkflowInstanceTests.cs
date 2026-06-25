namespace R2WAI.Domain.Tests.Entities;

public class WorkflowInstanceTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var instance = new WorkflowInstance(id, workflowId, tenantId, userId, "some data");

        Assert.Equal(id, instance.Id);
        Assert.Equal(workflowId, instance.WorkflowId);
        Assert.Equal(tenantId, instance.TenantId);
        Assert.Equal(userId, instance.InitiatedBy);
        Assert.Equal("some data", instance.Data);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.NotNull(instance.StartedAt);
    }

    [Fact]
    public void AdvanceStep_IncrementsCurrentStep()
    {
        var instance = CreateDefault();
        instance.AdvanceStep();
        Assert.Equal(1, instance.CurrentStep);
        instance.AdvanceStep();
        Assert.Equal(2, instance.CurrentStep);
    }

    [Fact]
    public void Complete_SetsCompletedStatus()
    {
        var instance = CreateDefault();
        instance.Complete();
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.NotNull(instance.CompletedAt);
    }

    [Fact]
    public void Fail_SetsFailedStatus()
    {
        var instance = CreateDefault();
        instance.Fail();
        Assert.Equal(WorkflowInstanceStatus.Failed, instance.Status);
        Assert.NotNull(instance.CompletedAt);
    }

    [Fact]
    public void Cancel_SetsCancelledStatus()
    {
        var instance = CreateDefault();
        instance.Cancel();
        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
        Assert.NotNull(instance.CompletedAt);
    }

    [Fact]
    public void SetElsaInstanceId_SetsId()
    {
        var instance = CreateDefault();
        instance.SetElsaInstanceId("elsa-123");
        Assert.Equal("elsa-123", instance.ElsaInstanceId);
    }

    [Fact]
    public void UpdateData_ChangesData()
    {
        var instance = CreateDefault();
        instance.UpdateData("updated data");
        Assert.Equal("updated data", instance.Data);
    }

    private static WorkflowInstance CreateDefault()
    {
        return new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }
}
