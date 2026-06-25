using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Workflows;

[Activity("R2WAI", "Approvals", "Creates an approval request and pauses the workflow until the request is approved or rejected.")]
public class ApprovalStepActivity : Activity<string>
{
    [Input(Description = "The tenant ID.")]
    public Input<string> TenantId { get; set; } = default!;

    [Input(Description = "The workflow instance ID.")]
    public Input<string> WorkflowInstanceId { get; set; } = default!;

    [Input(Description = "The workflow definition ID.")]
    public Input<string> WorkflowDefinitionId { get; set; } = default!;

    [Input(Description = "The user ID of the person requesting the approval.")]
    public Input<string> RequesterId { get; set; } = default!;

    [Input(Description = "Optional JSON payload to attach to the approval request.")]
    public Input<string?> Data { get; set; } = default!;

    [Output(Description = "The approval status: Approved, Rejected, or Cancelled.")]
    public Output<string> ApprovalStatus { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var tenantIdStr = context.Get(TenantId);
        var workflowInstanceIdStr = context.Get(WorkflowInstanceId);
        var workflowDefinitionIdStr = context.Get(WorkflowDefinitionId);
        var requesterIdStr = context.Get(RequesterId);

        if (tenantIdStr is null || workflowInstanceIdStr is null || workflowDefinitionIdStr is null || requesterIdStr is null)
        {
            context.Set(ApprovalStatus, "Failed");
            context.Set(Result, "Missing required input");
            await context.CompleteActivityAsync();
            return;
        }

        var tenantId = Guid.Parse(tenantIdStr);
        var workflowInstanceId = Guid.Parse(workflowInstanceIdStr);
        var workflowDefinitionId = Guid.Parse(workflowDefinitionIdStr);
        var requesterId = Guid.Parse(requesterIdStr);
        var data = context.Get(Data);

        var approvalService = context.GetRequiredService<IApprovalService>();

        var approvalRequestId = await approvalService.CreateApprovalRequestAsync(
            tenantId, workflowInstanceId, workflowDefinitionId, requesterId, data,
            context.CancellationToken);

        var stimulus = new ApprovalStimulus(approvalRequestId.ToString());
        context.CreateBookmark(stimulus, ResumeAsync, includeActivityInstanceId: false);
    }

    private async ValueTask ResumeAsync(ActivityExecutionContext context)
    {
        var stimulus = context.Bookmarks.FirstOrDefault()?.GetPayload<ApprovalStimulus>();
        if (stimulus is null)
        {
            context.Set(ApprovalStatus, "Approved");
            context.Set(Result, "Approved");
            await context.CompleteActivityAsync();
            return;
        }

        var approvalRequestId = Guid.Parse(stimulus.ApprovalRequestId);
        var dbContext = context.GetRequiredService<ApplicationDbContext>();

        var approvalRequest = await dbContext.ApprovalRequests
            .FirstOrDefaultAsync(ar => ar.Id == approvalRequestId, context.CancellationToken);

        var status = approvalRequest?.Status.ToString() ?? "Approved";

        context.Set(ApprovalStatus, status);
        context.Set(Result, status);
        await context.CompleteActivityAsync();
    }
}

public sealed record ApprovalStimulus(string ApprovalRequestId);
