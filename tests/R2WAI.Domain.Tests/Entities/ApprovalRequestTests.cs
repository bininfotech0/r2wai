namespace R2WAI.Domain.Tests.Entities;

public class ApprovalRequestTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var request = new ApprovalRequest(id, tenantId, instanceId, workflowId, requesterId, "test data");

        Assert.Equal(id, request.Id);
        Assert.Equal(tenantId, request.TenantId);
        Assert.Equal(instanceId, request.WorkflowInstanceId);
        Assert.Equal(requesterId, request.RequesterId);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
        Assert.Equal("test data", request.Data);
        Assert.Null(request.ApproverId);
    }

    [Fact]
    public void AssignApprover_SetsApproverAndRole()
    {
        var request = CreateDefault();
        var approverId = Guid.NewGuid();
        request.AssignApprover(approverId, "Admin");

        Assert.Equal(approverId, request.ApproverId);
        Assert.Equal("Admin", request.ApproverRole);
    }

    [Fact]
    public void Approve_SetsStatusAndComments()
    {
        var request = CreateDefault();
        request.Approve("Looks good");

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal("Looks good", request.Comments);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public void Reject_SetsStatusAndComments()
    {
        var request = CreateDefault();
        request.Reject("Needs revision");

        Assert.Equal(ApprovalStatus.Rejected, request.Status);
        Assert.Equal("Needs revision", request.Comments);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public void Escalate_IncrementsLevel()
    {
        var request = CreateDefault();
        request.Escalate();
        Assert.Equal(ApprovalStatus.Escalated, request.Status);
        Assert.Equal(1, request.EscalationLevel);

        request.Escalate();
        Assert.Equal(2, request.EscalationLevel);
    }

    [Fact]
    public void Cancel_SetsCancelledStatus()
    {
        var request = CreateDefault();
        request.Cancel();
        Assert.Equal(ApprovalStatus.Cancelled, request.Status);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public void SetElsaBookmarkId_SetsBookmarkId()
    {
        var request = CreateDefault();
        request.SetElsaBookmarkId("bookmark-123");
        Assert.Equal("bookmark-123", request.ElsaBookmarkId);
    }

    private static ApprovalRequest CreateDefault()
    {
        return new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }
}
