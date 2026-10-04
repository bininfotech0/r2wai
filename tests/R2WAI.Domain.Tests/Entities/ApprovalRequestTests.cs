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
    public void Create_WithoutAWorkflow_IsAPendingRequestWithASubject()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), workflowInstanceId: null, workflowId: null,
            requesterId: Guid.NewGuid(), subject: "Submit supplier ABC Industries");

        Assert.Null(request.WorkflowInstanceId);
        Assert.Null(request.WorkflowId);
        Assert.Equal("Submit supplier ABC Industries", request.Subject);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
    }

    [Fact]
    public void An_overlong_subject_is_capped_rather_than_failing_the_request()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(),
            subject: new string('x', ApprovalRequest.MaxSubjectLength + 50));

        Assert.Equal(ApprovalRequest.MaxSubjectLength, request.Subject!.Length);
    }

    [Fact]
    public void A_request_without_a_workflow_goes_through_the_same_decisions()
    {
        var approved = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        approved.Approve("ok");
        Assert.Equal(ApprovalStatus.Approved, approved.Status);

        var rejected = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        rejected.Reject("no");
        Assert.Equal(ApprovalStatus.Rejected, rejected.Status);

        var escalated = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        escalated.Escalate();
        Assert.Equal(ApprovalStatus.Escalated, escalated.Status);
    }

    [Fact]
    public void RecordDeferredExecutionResult_WithNoPriorComments_SetsComments()
    {
        var request = CreateDefault();
        request.Approve();

        request.RecordDeferredExecutionResult("{\"status\":\"ok\"}");

        Assert.Equal("Executed: {\"status\":\"ok\"}", request.Comments);
    }

    [Fact]
    public void RecordDeferredExecutionResult_AppendsToAnExistingApprovalComment_DoesNotOverwriteIt()
    {
        var request = CreateDefault();
        request.Approve("Looks fine to me");

        request.RecordDeferredExecutionResult("done");

        Assert.Equal("Looks fine to me\n\nExecuted: done", request.Comments);
    }

    private static ApprovalRequest CreateDefault()
    {
        return new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }
}
