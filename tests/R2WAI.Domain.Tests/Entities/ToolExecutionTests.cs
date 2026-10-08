using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Tests.Entities;

public class ToolExecutionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ToolId = Guid.NewGuid();

    [Fact]
    public void Denied_IsAlreadyComplete_WithItsReason()
    {
        var execution = ToolExecution.Denied(TenantId, UserId, ToolId, "Tools", "create_order", "missing required role 'Finance'");

        Assert.Equal(ToolExecutionStatus.Denied, execution.Status);
        Assert.Equal("missing required role 'Finance'", execution.DenialReason);
        Assert.NotNull(execution.CompletedAt);
    }

    [Fact]
    public void DirectCall_GoesPreparedThenSucceeded_WithDuration()
    {
        var execution = ToolExecution.Prepared(TenantId, UserId, ToolId, "Tools", "create_order");
        Assert.Equal(ToolExecutionStatus.Prepared, execution.Status);
        Assert.Null(execution.CompletedAt);

        execution.Succeed(durationMs: 120);

        Assert.Equal(ToolExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(120, execution.DurationMs);
        Assert.NotNull(execution.CompletedAt);
    }

    [Fact]
    public void FailedAndUnknown_KeepTheErrorText()
    {
        var failed = ToolExecution.Prepared(TenantId, UserId, ToolId, "Tools", "create_order");
        failed.Fail(30, "upstream 502");
        Assert.Equal(ToolExecutionStatus.Failed, failed.Status);
        Assert.Equal("upstream 502", failed.Error);

        var unknown = ToolExecution.Prepared(TenantId, UserId, ToolId, "Tools", "create_order");
        unknown.MarkUnknown(90_000, "The operation was canceled.");
        Assert.Equal(ToolExecutionStatus.Unknown, unknown.Status);
    }

    [Fact]
    public void PausedCall_IsPreparedOnlyAfterApproval_OrDeniedWhenRejected()
    {
        var approvalId = Guid.NewGuid();
        var approved = ToolExecution.AwaitingApproval(TenantId, UserId, ToolId, "Tools", "create_order", approvalId);
        Assert.Equal(approvalId, approved.ApprovalRequestId);

        approved.PrepareAfterApproval();
        approved.Succeed(10);
        Assert.Equal(ToolExecutionStatus.Succeeded, approved.Status);

        var rejected = ToolExecution.AwaitingApproval(TenantId, UserId, ToolId, "Tools", "create_order", approvalId);
        rejected.RejectApproval("confirmation rejected");
        Assert.Equal(ToolExecutionStatus.Denied, rejected.Status);
        Assert.Equal("confirmation rejected", rejected.DenialReason);
    }

    [Fact]
    public void IllegalTransitions_Throw_SoTheLedgerCannotBeRewritten()
    {
        var paused = ToolExecution.AwaitingApproval(TenantId, UserId, ToolId, "Tools", "create_order", Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => paused.Succeed(1)); // must be approved first

        var done = ToolExecution.Prepared(TenantId, UserId, ToolId, "Tools", "create_order");
        done.Succeed(1);
        Assert.Throws<InvalidOperationException>(() => done.Fail(1, "late")); // a finished row is final
        Assert.Throws<InvalidOperationException>(() => done.PrepareAfterApproval());
    }
}
