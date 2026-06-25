namespace R2WAI.Domain.Tests.Entities;

public class WorkflowStepExecutionTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        var step = new WorkflowStepExecution(id, instanceId, 0, "Approval Step", "Approval");

        Assert.Equal(id, step.Id);
        Assert.Equal(instanceId, step.WorkflowInstanceId);
        Assert.Equal(0, step.StepIndex);
        Assert.Equal("Approval Step", step.StepName);
        Assert.Equal("Approval", step.StepType);
        Assert.Equal(WorkflowStepStatus.Pending, step.Status);
        Assert.Null(step.StartedAt);
        Assert.Null(step.CompletedAt);
        Assert.Null(step.Output);
        Assert.Null(step.Error);
        Assert.Null(step.Variables);
    }

    [Fact]
    public void Create_WithoutStepType_DefaultsToNull()
    {
        var step = new WorkflowStepExecution(Guid.NewGuid(), Guid.NewGuid(), 1, "Step 1");

        Assert.Null(step.StepType);
        Assert.Equal(WorkflowStepStatus.Pending, step.Status);
    }

    [Fact]
    public void Start_SetsRunningStatusAndStartTime()
    {
        var step = CreateDefault();
        var before = DateTime.UtcNow;

        step.Start();

        Assert.Equal(WorkflowStepStatus.Running, step.Status);
        Assert.NotNull(step.StartedAt);
        Assert.True(step.StartedAt >= before);
        Assert.Null(step.CompletedAt);
    }

    [Fact]
    public void Complete_SetsCompletedStatusAndTime()
    {
        var step = CreateDefault();
        step.Start();

        step.Complete("Result data", "{\"key\": \"value\"}");

        Assert.Equal(WorkflowStepStatus.Completed, step.Status);
        Assert.NotNull(step.CompletedAt);
        Assert.Equal("Result data", step.Output);
        Assert.Equal("{\"key\": \"value\"}", step.Variables);
        Assert.Null(step.Error);
    }

    [Fact]
    public void Complete_WithoutOutput_SetsNullOutput()
    {
        var step = CreateDefault();
        step.Start();
        step.Complete();

        Assert.Equal(WorkflowStepStatus.Completed, step.Status);
        Assert.Null(step.Output);
        Assert.Null(step.Variables);
    }

    [Fact]
    public void Fail_SetsFailedStatusAndError()
    {
        var step = CreateDefault();
        step.Start();

        step.Fail("Connection timeout");

        Assert.Equal(WorkflowStepStatus.Failed, step.Status);
        Assert.NotNull(step.CompletedAt);
        Assert.Equal("Connection timeout", step.Error);
        Assert.Null(step.Output);
    }

    [Fact]
    public void Skip_SetsSkippedStatus()
    {
        var step = CreateDefault();

        step.Skip();

        Assert.Equal(WorkflowStepStatus.Skipped, step.Status);
        Assert.NotNull(step.CompletedAt);
    }

    [Fact]
    public void FullLifecycle_PendingToRunningToCompleted()
    {
        var step = CreateDefault();
        Assert.Equal(WorkflowStepStatus.Pending, step.Status);

        step.Start();
        Assert.Equal(WorkflowStepStatus.Running, step.Status);
        Assert.NotNull(step.StartedAt);

        step.Complete("Done");
        Assert.Equal(WorkflowStepStatus.Completed, step.Status);
        Assert.NotNull(step.CompletedAt);
        Assert.True(step.CompletedAt >= step.StartedAt);
    }

    [Fact]
    public void FullLifecycle_PendingToRunningToFailed()
    {
        var step = CreateDefault();

        step.Start();
        step.Fail("API returned 500");

        Assert.Equal(WorkflowStepStatus.Failed, step.Status);
        Assert.Equal("API returned 500", step.Error);
        Assert.NotNull(step.StartedAt);
        Assert.NotNull(step.CompletedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(99)]
    public void Create_WithDifferentStepIndexes_Works(int stepIndex)
    {
        var step = new WorkflowStepExecution(Guid.NewGuid(), Guid.NewGuid(),
            stepIndex, $"Step {stepIndex}");

        Assert.Equal(stepIndex, step.StepIndex);
    }

    private static WorkflowStepExecution CreateDefault()
    {
        return new WorkflowStepExecution(Guid.NewGuid(), Guid.NewGuid(),
            0, "Test Step", "Action");
    }
}
