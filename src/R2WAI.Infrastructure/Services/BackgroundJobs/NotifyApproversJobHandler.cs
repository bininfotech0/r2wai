using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.Services.BackgroundJobs;

/// <summary>
/// Emails + notifies every user in one of the payload's Roles about an approval request --
/// unifies what used to be two near-identical inline closures in ApprovalService (one for a
/// freshly-created request, one for an escalation to the next approval level). Approvers/
/// requester name/workflow name are all re-resolved here at handling time (not enqueue time),
/// matching the original closures' behavior.
///
/// Per-approver idempotent (implementation plan Phase 5): this handler runs inside a
/// BackgroundJob, and BackgroundJobProcessor now reclaims a job whose replica crashed mid-run
/// (its own lease-expiry fix, same phase) in addition to retrying on a thrown exception — both
/// paths re-run this entire method from the top. Without ApprovalNotificationDispatch's per-
/// approver marker, a crash or exception after approver #1's email/notification but before the
/// loop finished used to re-send to every already-notified approver on the next attempt.
/// </summary>
public sealed class NotifyApproversJobHandler(
    ApplicationDbContext dbContext, IEmailService emailService, INotificationService notificationService)
    : IBackgroundJobHandler
{
    public string JobType => BackgroundJobTypes.NotifyApprovers;

    public async Task HandleAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<NotifyApproversJobPayload>(payloadJson)
            ?? throw new InvalidOperationException($"Malformed {nameof(NotifyApproversJobPayload)}");

        // IgnoreQueryFilters: IBackgroundJobHandler implementations are only ever invoked from
        // BackgroundJobProcessor, a background IHostedService with no HttpContext/ambient tenant —
        // the ambient filter would otherwise AND a false "TenantId == null" onto this query's own
        // explicit u.TenantId == payload.TenantId check, making it always match nothing (P0-5's
        // fail-closed filter). payload.TenantId is the real, already-verified scope here, not the
        // ambient claim, so this is safe. Confirmed live: this silently broke every approval
        // notification (initial request and escalation) the moment fail-closed shipped — the job
        // "succeeded" (empty approvers list, no exception) so nothing ever surfaced the failure.
        var approvers = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted && u.TenantId == payload.TenantId && u.UserRoles.Any(ur => payload.Roles.Contains(ur.Role!.Name)))
            .ToListAsync(ct);

        // What the request is about: its own subject if it has one, else the workflow's name (a request
        // raised by a workflow step), else a neutral word. A request with no workflow has no workflow.
        var approval = await dbContext.ApprovalRequests.FindAsync([payload.ApprovalRequestId], ct);
        var workflow = payload.WorkflowId is { } workflowId ? await dbContext.Workflows.FindAsync([workflowId], ct) : null;
        var workflowName = approval?.Subject ?? workflow?.Name ?? (payload.WorkflowId is null ? "Request" : "Workflow");

        string requesterDisplay;
        string title;
        string bodySuffix;
        if (payload.RequesterId is { } requesterId)
        {
            var requester = await dbContext.Users.FindAsync([requesterId], ct);
            requesterDisplay = requester is not null ? $"{requester.FirstName} {requester.LastName}" : "Unknown";
            title = "Approval Required";
            bodySuffix = "";
        }
        else
        {
            requesterDisplay = "Previous level approved";
            title = $"Level {(payload.EscalationLevel ?? 0) + 1} Approval Required";
            bodySuffix = " (escalated from previous level)";
        }

        var escalationLevel = payload.EscalationLevel ?? 0;

        // IgnoreQueryFilters: same reasoning as the approvers query above — no ambient tenant here.
        var alreadyNotified = await dbContext.Set<ApprovalNotificationDispatch>()
            .IgnoreQueryFilters()
            .Where(d => d.ApprovalRequestId == payload.ApprovalRequestId && d.EscalationLevel == escalationLevel)
            .Select(d => d.ApproverId)
            .ToListAsync(ct);
        var alreadyNotifiedSet = alreadyNotified.ToHashSet();

        foreach (var approver in approvers)
        {
            if (alreadyNotifiedSet.Contains(approver.Id))
                continue; // this run is a retry of a partially-completed attempt — don't re-notify

            await emailService.SendApprovalRequestAsync(
                approver.Email, approver.FirstName, workflowName, requesterDisplay, payload.Data, payload.ApprovalRequestId, ct);

            await notificationService.SendAsync(approver.Id.ToString(), title, $"{workflowName} needs your approval{bodySuffix}", "approval", null, ct);

            // Written immediately after this approver's send, not batched at the loop's end — a
            // crash right here can still double-notify this one approver, but everyone before them
            // in the loop stays protected on the next retry.
            dbContext.Set<ApprovalNotificationDispatch>().Add(
                new ApprovalNotificationDispatch(Guid.NewGuid(), payload.TenantId, payload.ApprovalRequestId, escalationLevel, approver.Id));
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
