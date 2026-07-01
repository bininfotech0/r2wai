using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public sealed class ApprovalResult
{
    public bool IsSuccess { get; init; }
    public bool IsApproved { get; init; }
    public string? Comments { get; init; }
    public Guid? ApproverId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid ApprovalRequestId { get; init; }
    public string? Error { get; init; }
}

public sealed record ApprovalPolicyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    string? WorkflowType,
    string? ApproverRoles,
    int MinApprovers,
    int? EscalationMinutes,
    string? EscalationRoles,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt
);

public sealed record CreateApprovalPolicyRequest(
    string Name,
    string? Description,
    string? WorkflowType,
    string? ApproverRoles,
    int MinApprovers = 1,
    int? EscalationMinutes = null,
    string? EscalationRoles = null
);

public sealed record UpdateApprovalPolicyRequest(
    string Name,
    string? Description,
    string? WorkflowType,
    string? ApproverRoles,
    int MinApprovers = 1,
    int? EscalationMinutes = null,
    string? EscalationRoles = null
);

public sealed class PendingApprovalDto
{
    public Guid Id { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowName { get; init; } = string.Empty;
    public Guid RequesterId { get; init; }
    public string RequesterFirstName { get; init; } = string.Empty;
    public string RequesterLastName { get; init; } = string.Empty;
    public ApprovalStatus Status { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? DueAt { get; init; }
    public int EscalationLevel { get; init; }
    public string? Data { get; init; }
}

public interface IApprovalService
{
    Task<Guid> CreateApprovalRequestAsync(Guid tenantId, Guid workflowInstanceId, Guid workflowId, Guid requesterId, string? data = null, CancellationToken ct = default);
    Task<ApprovalResult> ApproveAsync(Guid requestId, Guid approverId, string? comments = null, CancellationToken ct = default);
    Task<ApprovalResult> RejectAsync(Guid requestId, Guid approverId, string? comments = null, CancellationToken ct = default);
    Task<List<PendingApprovalDto>> GetPendingForApproverAsync(Guid tenantId, Guid approverId, CancellationToken ct = default);
    Task<List<PendingApprovalDto>> GetPendingForRoleAsync(Guid tenantId, string role, CancellationToken ct = default);
    Task<(List<PendingApprovalDto> Items, int TotalCount)> GetPendingPagedAsync(Guid tenantId, Guid? approverId = null, string? role = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task EscalateOverdueAsync(CancellationToken ct = default);
    Task<ApprovalPolicy?> FindPolicyAsync(Guid tenantId, string? workflowType, CancellationToken ct = default);
    Task<List<ApprovalPolicyDto>> GetPoliciesAsync(Guid tenantId, bool? activeOnly = null, CancellationToken ct = default);
    Task<ApprovalPolicyDto> GetPolicyByIdAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);
    Task<ApprovalPolicyDto> CreatePolicyAsync(Guid tenantId, CreateApprovalPolicyRequest request, CancellationToken ct = default);
    Task<ApprovalPolicyDto> UpdatePolicyAsync(Guid tenantId, Guid policyId, UpdateApprovalPolicyRequest request, CancellationToken ct = default);
    Task DeletePolicyAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);
    Task<ApprovalPolicyDto> TogglePolicyActiveAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);
}

public class ApprovalService : IApprovalService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(ApplicationDbContext context, IEmailService emailService,
        INotificationService notificationService, IBackgroundTaskQueue taskQueue,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _emailService = emailService;
        _notificationService = notificationService;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    public async Task<Guid> CreateApprovalRequestAsync(Guid tenantId, Guid workflowInstanceId,
        Guid workflowId, Guid requesterId, string? data = null, CancellationToken ct = default)
    {
        var request = new ApprovalRequest(
            Guid.NewGuid(), tenantId, workflowInstanceId, workflowId, requesterId, data);

        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created approval request {RequestId} for workflow instance {InstanceId}",
            request.Id, workflowInstanceId);

        var requestId = request.Id;
        await _taskQueue.EnqueueAsync(async (sp, ct) =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var emailSvc = sp.GetRequiredService<IEmailService>();
            var notifySvc = sp.GetRequiredService<INotificationService>();

            var policy = await db.ApprovalPolicies
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .FirstOrDefaultAsync(ct);

            if (policy?.ApproverRoles is not null)
            {
                var roles = policy.ApproverRoles.Split(',', StringSplitOptions.TrimEntries);
                var approvers = await db.Users
                    .Where(u => u.TenantId == tenantId && u.UserRoles.Any(ur => roles.Contains(ur.Role!.Name)))
                    .ToListAsync(ct);

                var requester = await db.Users.FindAsync([requesterId], ct);
                var workflow = await db.Workflows.FindAsync([workflowId], ct);

                foreach (var approver in approvers)
                {
                    await emailSvc.SendApprovalRequestAsync(
                        approver.Email, approver.FirstName, workflow?.Name ?? "Workflow",
                        requester is not null ? $"{requester.FirstName} {requester.LastName}" : "Unknown",
                        data, requestId, ct);

                    await notifySvc.SendAsync(approver.Id.ToString(),
                        "Approval Required", $"{workflow?.Name ?? "Workflow"} needs your approval",
                        "approval", null, ct);
                }
            }
        });

        return request.Id;
    }

    public async Task<ApprovalResult> ApproveAsync(Guid requestId, Guid approverId,
        string? comments = null, CancellationToken ct = default)
    {
        var request = await _context.ApprovalRequests
            .FirstOrDefaultAsync(ar => ar.Id == requestId, ct);

        if (request is null)
            throw new NotFoundException(nameof(ApprovalRequest), requestId);

        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Approval request {requestId} is not in Pending status");

        if (request.ApproverId.HasValue && request.ApproverId.Value != approverId)
            throw new UnauthorizedAccessException($"Approval request {requestId} is assigned to a different approver");

        if (!request.ApproverId.HasValue)
        {
            var isAuthorized = await VerifyApproverAuthorization(request, approverId, ct);
            if (!isAuthorized)
                throw new UnauthorizedAccessException($"User {approverId} is not authorized to approve request {requestId}");
        }

        request.AssignApprover(approverId);
        request.Approve(comments);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Approval request {RequestId} approved by {ApproverId} at level {Level}",
            requestId, approverId, request.ApprovalLevel);

        var nextLevelCreated = await CreateNextLevelApprovalAsync(request, ct);

        if (!nextLevelCreated)
            _ = NotifyRequesterOfDecisionAsync(request, true, comments);

        return new ApprovalResult
        {
            IsSuccess = true,
            IsApproved = true,
            Comments = comments,
            ApproverId = approverId,
            WorkflowInstanceId = request.WorkflowInstanceId,
            ApprovalRequestId = request.Id
        };
    }

    public async Task<ApprovalResult> RejectAsync(Guid requestId, Guid approverId,
        string? comments = null, CancellationToken ct = default)
    {
        var request = await _context.ApprovalRequests
            .FirstOrDefaultAsync(ar => ar.Id == requestId, ct);

        if (request is null)
            throw new NotFoundException(nameof(ApprovalRequest), requestId);

        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Approval request {requestId} is not in Pending status");

        if (request.ApproverId.HasValue && request.ApproverId.Value != approverId)
            throw new UnauthorizedAccessException($"Approval request {requestId} is assigned to a different approver");

        if (!request.ApproverId.HasValue)
        {
            var isAuthorized = await VerifyApproverAuthorization(request, approverId, ct);
            if (!isAuthorized)
                throw new UnauthorizedAccessException($"User {approverId} is not authorized to reject request {requestId}");
        }

        request.AssignApprover(approverId);
        request.Reject(comments);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Approval request {RequestId} rejected by {ApproverId}", requestId, approverId);

        _ = NotifyRequesterOfDecisionAsync(request, false, comments);

        return new ApprovalResult
        {
            IsSuccess = true,
            IsApproved = false,
            Comments = comments,
            ApproverId = approverId,
            WorkflowInstanceId = request.WorkflowInstanceId,
            ApprovalRequestId = request.Id
        };
    }

    private async Task<bool> VerifyApproverAuthorization(ApprovalRequest request, Guid approverId, CancellationToken ct)
    {
        var policy = await _context.ApprovalPolicies
            .Where(p => p.TenantId == request.TenantId && p.IsActive)
            .FirstOrDefaultAsync(ct);

        if (policy?.ApproverRoles is null)
            return false;

        var roles = policy.ApproverRoles.Split(',', StringSplitOptions.TrimEntries);
        return await _context.Users
            .AnyAsync(u => u.Id == approverId &&
                u.TenantId == request.TenantId &&
                u.UserRoles.Any(ur => roles.Contains(ur.Role!.Name)), ct);
    }

    public async Task<List<PendingApprovalDto>> GetPendingForApproverAsync(
        Guid tenantId, Guid approverId, CancellationToken ct = default)
    {
        return await _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId && ar.ApproverId == approverId && ar.Status == ApprovalStatus.Pending)
            .OrderBy(ar => ar.RequestedAt)
            .Select(ar => new PendingApprovalDto
            {
                Id = ar.Id,
                WorkflowInstanceId = ar.WorkflowInstanceId,
                WorkflowId = ar.WorkflowId,
                WorkflowName = ar.Workflow.Name,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data
            })
            .ToListAsync(ct);
    }

    public async Task<List<PendingApprovalDto>> GetPendingForRoleAsync(
        Guid tenantId, string role, CancellationToken ct = default)
    {
        return await _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId && ar.ApproverRole == role && ar.Status == ApprovalStatus.Pending)
            .OrderBy(ar => ar.RequestedAt)
            .Select(ar => new PendingApprovalDto
            {
                Id = ar.Id,
                WorkflowInstanceId = ar.WorkflowInstanceId,
                WorkflowId = ar.WorkflowId,
                WorkflowName = ar.Workflow.Name,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data
            })
            .ToListAsync(ct);
    }

    public async Task<(List<PendingApprovalDto> Items, int TotalCount)> GetPendingPagedAsync(
        Guid tenantId, Guid? approverId = null, string? role = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId && ar.Status == ApprovalStatus.Pending);

        if (approverId.HasValue)
            query = query.Where(ar => ar.ApproverId == approverId.Value);
        if (!string.IsNullOrEmpty(role))
            query = query.Where(ar => ar.ApproverRole == role);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(ar => ar.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ar => new PendingApprovalDto
            {
                Id = ar.Id,
                WorkflowInstanceId = ar.WorkflowInstanceId,
                WorkflowId = ar.WorkflowId,
                WorkflowName = ar.Workflow.Name,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task EscalateOverdueAsync(CancellationToken ct = default)
    {
        // IgnoreQueryFilters: this is a cross-tenant background job — the global tenant filter
        // would silently match nothing here because there is no HttpContext. Soft-delete is
        // re-applied manually since IgnoreQueryFilters also bypasses that filter.
        var overdue = await _context.ApprovalRequests
            .IgnoreQueryFilters()
            .Where(ar => !ar.IsDeleted && ar.Status == ApprovalStatus.Pending && ar.DueAt != null && ar.DueAt < DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var request in overdue)
        {
            request.Escalate();

            var policy = await _context.ApprovalPolicies
                .Where(ap => ap.TenantId == request.TenantId && ap.WorkflowType == null && ap.IsActive)
                .FirstOrDefaultAsync(ct);

            if (policy?.EscalationRoles != null && request.ApproverRole != policy.EscalationRoles)
            {
                request.AssignApprover(request.ApproverId ?? Guid.Empty, policy.EscalationRoles);
            }

            _logger.LogWarning("Escalated approval request {RequestId} to level {Level}",
                request.Id, request.EscalationLevel);
        }

        if (overdue.Count > 0)
            await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ApprovalPolicyDto>> GetPoliciesAsync(Guid tenantId, bool? activeOnly = null,
        CancellationToken ct = default)
    {
        var query = _context.ApprovalPolicies
            .Where(ap => ap.TenantId == tenantId);

        if (activeOnly.HasValue)
            query = query.Where(ap => ap.IsActive == activeOnly.Value);

        return await query
            .OrderByDescending(ap => ap.CreatedAt)
            .Select(ap => new ApprovalPolicyDto(
                ap.Id, ap.TenantId, ap.Name, ap.Description,
                ap.WorkflowType, ap.ApproverRoles, ap.MinApprovers,
                ap.EscalationMinutes, ap.EscalationRoles,
                ap.IsActive, ap.CreatedAt, ap.ModifiedAt))
            .ToListAsync(ct);
    }

    public async Task<ApprovalPolicyDto> GetPolicyByIdAsync(Guid tenantId, Guid policyId,
        CancellationToken ct = default)
    {
        var policy = await _context.ApprovalPolicies
            .FirstOrDefaultAsync(ap => ap.Id == policyId && ap.TenantId == tenantId, ct);

        if (policy is null)
            throw new NotFoundException(nameof(ApprovalPolicy), policyId);

        return new ApprovalPolicyDto(
            policy.Id, policy.TenantId, policy.Name, policy.Description,
            policy.WorkflowType, policy.ApproverRoles, policy.MinApprovers,
            policy.EscalationMinutes, policy.EscalationRoles,
            policy.IsActive, policy.CreatedAt, policy.ModifiedAt);
    }

    public async Task<ApprovalPolicyDto> CreatePolicyAsync(Guid tenantId,
        CreateApprovalPolicyRequest request, CancellationToken ct = default)
    {
        var policy = new ApprovalPolicy(
            Guid.NewGuid(), tenantId, request.Name, request.Description,
            request.WorkflowType, request.ApproverRoles,
            request.MinApprovers, request.EscalationMinutes, request.EscalationRoles);

        _context.ApprovalPolicies.Add(policy);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created approval policy {PolicyId}: {Name}", policy.Id, request.Name);

        return new ApprovalPolicyDto(
            policy.Id, policy.TenantId, policy.Name, policy.Description,
            policy.WorkflowType, policy.ApproverRoles, policy.MinApprovers,
            policy.EscalationMinutes, policy.EscalationRoles,
            policy.IsActive, policy.CreatedAt, policy.ModifiedAt);
    }

    public async Task<ApprovalPolicyDto> UpdatePolicyAsync(Guid tenantId, Guid policyId,
        UpdateApprovalPolicyRequest request, CancellationToken ct = default)
    {
        var policy = await _context.ApprovalPolicies
            .FirstOrDefaultAsync(ap => ap.Id == policyId && ap.TenantId == tenantId, ct);

        if (policy is null)
            throw new NotFoundException(nameof(ApprovalPolicy), policyId);

        policy.Update(request.Name, request.Description, request.WorkflowType,
            request.ApproverRoles, request.MinApprovers,
            request.EscalationMinutes, request.EscalationRoles);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated approval policy {PolicyId}: {Name}", policyId, request.Name);

        return new ApprovalPolicyDto(
            policy.Id, policy.TenantId, policy.Name, policy.Description,
            policy.WorkflowType, policy.ApproverRoles, policy.MinApprovers,
            policy.EscalationMinutes, policy.EscalationRoles,
            policy.IsActive, policy.CreatedAt, policy.ModifiedAt);
    }

    public async Task DeletePolicyAsync(Guid tenantId, Guid policyId,
        CancellationToken ct = default)
    {
        var policy = await _context.ApprovalPolicies
            .FirstOrDefaultAsync(ap => ap.Id == policyId && ap.TenantId == tenantId, ct);

        if (policy is null)
            throw new NotFoundException(nameof(ApprovalPolicy), policyId);

        _context.ApprovalPolicies.Remove(policy);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted approval policy {PolicyId}", policyId);
    }

    public async Task<ApprovalPolicyDto> TogglePolicyActiveAsync(Guid tenantId, Guid policyId,
        CancellationToken ct = default)
    {
        var policy = await _context.ApprovalPolicies
            .FirstOrDefaultAsync(ap => ap.Id == policyId && ap.TenantId == tenantId, ct);

        if (policy is null)
            throw new NotFoundException(nameof(ApprovalPolicy), policyId);

        if (policy.IsActive)
            policy.Deactivate();
        else
            policy.Activate();

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Toggled approval policy {PolicyId} active={IsActive}", policyId, policy.IsActive);

        return new ApprovalPolicyDto(
            policy.Id, policy.TenantId, policy.Name, policy.Description,
            policy.WorkflowType, policy.ApproverRoles, policy.MinApprovers,
            policy.EscalationMinutes, policy.EscalationRoles,
            policy.IsActive, policy.CreatedAt, policy.ModifiedAt);
    }

    public async Task<ApprovalPolicy?> FindPolicyAsync(Guid tenantId, string? workflowType,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(workflowType))
        {
            var policy = await _context.ApprovalPolicies
                .Where(ap => ap.TenantId == tenantId && ap.WorkflowType == workflowType && ap.IsActive)
                .FirstOrDefaultAsync(ct);
            if (policy is not null)
                return policy;
        }

        return await _context.ApprovalPolicies
            .Where(ap => ap.TenantId == tenantId && ap.WorkflowType == null && ap.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> CreateNextLevelApprovalAsync(ApprovalRequest completedRequest, CancellationToken ct)
    {
        var policy = await FindPolicyAsync(completedRequest.TenantId, null, ct);
        if (policy?.ApproverRoles is null)
            return false;

        var roleChain = policy.ApproverRoles.Split(',', StringSplitOptions.TrimEntries);
        var nextLevel = completedRequest.ApprovalLevel + 1;

        if (nextLevel >= roleChain.Length)
            return false;

        var nextRole = roleChain[nextLevel];
        var nextApproval = new ApprovalRequest(
            Guid.NewGuid(), completedRequest.TenantId,
            completedRequest.WorkflowInstanceId, completedRequest.WorkflowId,
            completedRequest.RequesterId, completedRequest.Data, null,
            nextLevel, completedRequest.Id);
        nextApproval.AssignApprover(Guid.Empty, nextRole);

        _context.ApprovalRequests.Add(nextApproval);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created next-level approval {NextId} at level {Level} for role {Role}",
            nextApproval.Id, nextLevel, nextRole);

        var nextApprovalId = nextApproval.Id;
        var nextApprovalTenantId = completedRequest.TenantId;
        var nextApprovalWorkflowId = completedRequest.WorkflowId;
        var nextApprovalData = completedRequest.Data;
        await _taskQueue.EnqueueAsync(async (sp, ct) =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var emailSvc = sp.GetRequiredService<IEmailService>();
            var notifySvc = sp.GetRequiredService<INotificationService>();

            var approvers = await db.Users
                .Where(u => u.TenantId == nextApprovalTenantId &&
                            u.UserRoles.Any(ur => ur.Role!.Name == nextRole))
                .ToListAsync(ct);

            var workflow = await db.Workflows.FindAsync([nextApprovalWorkflowId], ct);

            foreach (var approver in approvers)
            {
                await emailSvc.SendApprovalRequestAsync(
                    approver.Email, approver.FirstName, workflow?.Name ?? "Workflow",
                    "Previous level approved", nextApprovalData,
                    nextApprovalId, ct);

                await notifySvc.SendAsync(approver.Id.ToString(),
                    $"Level {nextLevel + 1} Approval Required",
                    $"{workflow?.Name ?? "Workflow"} needs your approval (escalated from previous level)",
                    "approval", null, ct);
            }
        });

        return true;
    }

    private async Task NotifyRequesterOfDecisionAsync(ApprovalRequest request, bool approved, string? comments)
    {
        try
        {
            var requester = await _context.Users.FindAsync(request.RequesterId);
            var workflow = await _context.Workflows.FindAsync(request.WorkflowId);
            if (requester is not null)
            {
                await _emailService.SendApprovalDecisionAsync(
                    requester.Email, requester.FirstName, workflow?.Name ?? "Workflow",
                    approved, comments, CancellationToken.None);

                await _notificationService.SendAsync(requester.Id.ToString(),
                    approved ? "Request Approved" : "Request Rejected",
                    $"Your request for {workflow?.Name ?? "Workflow"} was {(approved ? "approved" : "rejected")}",
                    approved ? "success" : "error", null, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify requester for approval {ApprovalId}", request.Id);
        }
    }
}
