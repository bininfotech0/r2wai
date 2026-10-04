using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;

namespace R2WAI.Infrastructure.Services;

public sealed class ApprovalResult
{
    public bool IsSuccess { get; init; }
    public bool IsApproved { get; init; }
    public string? Comments { get; init; }
    public Guid? ApproverId { get; init; }
    // Null when the request was not raised by a workflow step.
    public Guid? WorkflowInstanceId { get; init; }
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
    // The three workflow fields are null for a request that was not raised by a workflow step.
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? WorkflowId { get; init; }
    public string? WorkflowName { get; init; }
    // What is being decided, in words. Preferred over WorkflowName wherever a title is shown.
    public string? Subject { get; init; }
    // Real FK, one hop via the Workflow definition — ApprovalRequest itself has no direct
    // ApplicationId. No equivalent Capability/ToolDefinition link exists anywhere on this
    // entity (a Capability field is deliberately not added — nothing real to show).
    public Guid? ApplicationId { get; init; }
    public string? ApplicationName { get; init; }
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
    Task<Guid> CreateApprovalRequestAsync(Guid tenantId, Guid? workflowInstanceId, Guid? workflowId, Guid requesterId, string? data = null, string? subject = null, CancellationToken ct = default);
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
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(ApplicationDbContext context, IEmailService emailService,
        INotificationService notificationService, IBackgroundJobQueue jobQueue,
        IServiceScopeFactory scopeFactory, ILogger<ApprovalService> logger)
    {
        _context = context;
        _emailService = emailService;
        _notificationService = notificationService;
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Guid> CreateApprovalRequestAsync(Guid tenantId, Guid? workflowInstanceId,
        Guid? workflowId, Guid requesterId, string? data = null, string? subject = null, CancellationToken ct = default)
    {
        // DueAt drives EscalateOverdueAsync (the 5-minute background sweep) — without it set here,
        // every approval request stays DueAt=null forever and escalation can never trigger for it,
        // no matter how long it sits pending. Computed from the tenant's active policy up front so
        // it's set at creation, not patched in later.
        var creationPolicy = await _context.ApprovalPolicies
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .FirstOrDefaultAsync(ct);
        var dueAt = creationPolicy?.EscalationMinutes is int minutes ? DateTime.UtcNow.AddMinutes(minutes) : (DateTime?)null;

        var request = new ApprovalRequest(
            Guid.NewGuid(), tenantId, workflowInstanceId, workflowId, requesterId, data, dueAt, subject: subject);

        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created approval request {RequestId} (workflow instance {InstanceId})",
            request.Id, workflowInstanceId);

        if (creationPolicy?.ApproverRoles is not null)
        {
            var roles = ParseRoles(creationPolicy.ApproverRoles);
            await _jobQueue.EnqueueAsync(BackgroundJobTypes.NotifyApprovers,
                new NotifyApproversJobPayload(request.Id, tenantId, workflowId, roles, requesterId, data, EscalationLevel: null), ct);
        }

        return request.Id;
    }

    public async Task<ApprovalResult> ApproveAsync(Guid requestId, Guid approverId,
        string? comments = null, CancellationToken ct = default)
    {
        var request = await _context.ApprovalRequests
            .FirstOrDefaultAsync(ar => ar.Id == requestId, ct);

        if (request is null)
            throw new NotFoundException(nameof(ApprovalRequest), requestId);

        if (!IsAwaitingDecision(request.Status))
            throw new InvalidOperationException($"Approval request {requestId} is not in Pending status");

        // Separation of duties: a requester deciding their own request defeats the point of asking for
        // approval at all. Checked before the assigned-approver/role checks below, so it applies
        // regardless of *how* the caller would otherwise have been authorized (explicitly assigned, or
        // via role membership) — this was P0's "requester != approver" finding, previously unenforced.
        if (approverId == request.RequesterId)
            throw new UnauthorizedAccessException($"User {approverId} requested approval {requestId} and cannot decide it themselves");

        var assignedApprover = AssignedApprover(request);
        if (assignedApprover.HasValue && assignedApprover.Value != approverId)
            throw new UnauthorizedAccessException($"Approval request {requestId} is assigned to a different approver");

        if (!assignedApprover.HasValue)
        {
            var isAuthorized = await VerifyApproverAuthorization(request, approverId, ct);
            if (!isAuthorized)
                throw new UnauthorizedAccessException($"User {approverId} is not authorized to approve request {requestId}");
        }

        // Atomic claim: without this, two concurrent Approve calls for the same request (a UI
        // double-click, a retried HTTP request) can both pass the IsAwaitingDecision check above,
        // both flip to Approved, and both save — the same race class as P0-3's sweepers, but now with
        // a sharper edge: ApprovalsController.Approve can trigger a real deferred tool-call execution
        // (DeferredToolCallExecutor) for a non-workflow approval, so a double-approve race here means
        // an approved-but-not-yet-idempotent write operation could genuinely run twice. See
        // EscalateOverdueAsync for the same idiom and the same InMemory-provider (test-only) fallback.
        await ClaimDecisionAsync(requestId, ApprovalStatus.Approved, ct);

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

        if (!IsAwaitingDecision(request.Status))
            throw new InvalidOperationException($"Approval request {requestId} is not in Pending status");

        // Separation of duties — see ApproveAsync's identical check for the full reasoning.
        if (approverId == request.RequesterId)
            throw new UnauthorizedAccessException($"User {approverId} requested approval {requestId} and cannot decide it themselves");

        var assignedApprover = AssignedApprover(request);
        if (assignedApprover.HasValue && assignedApprover.Value != approverId)
            throw new UnauthorizedAccessException($"Approval request {requestId} is assigned to a different approver");

        if (!assignedApprover.HasValue)
        {
            var isAuthorized = await VerifyApproverAuthorization(request, approverId, ct);
            if (!isAuthorized)
                throw new UnauthorizedAccessException($"User {approverId} is not authorized to reject request {requestId}");
        }

        // See ApproveAsync's identical guard — same race, same idiom, kept symmetric even though
        // rejection has no deferred-execution side effect today (double-notification is the only
        // current downside here, but the two decision paths should not silently diverge).
        await ClaimDecisionAsync(requestId, ApprovalStatus.Rejected, ct);

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

    // Pending and Escalated are both "waiting on a human": escalation only re-routes a late request, it
    // must never make the request undecidable.
    private static bool IsAwaitingDecision(ApprovalStatus status) =>
        status is ApprovalStatus.Pending or ApprovalStatus.Escalated;

    // Atomic claim for ApproveAsync/RejectAsync: a conditional UPDATE that only succeeds if the request
    // is still awaiting a decision, same idiom as EscalateOverdueAsync/BackgroundJobProcessor. Throws
    // the same "not in Pending status" InvalidOperationException the pre-existing IsAwaitingDecision
    // check already throws for the non-racing case, so callers see one consistent error either way.
    // ExecuteUpdateAsync is relational-only (the EF Core InMemory provider used by the fast API test
    // suite doesn't support it) — real deployments are always relational, so the guard only ever
    // matters for the atomic path; InMemory tests fall back to relying on the check already done above.
    private async Task ClaimDecisionAsync(Guid requestId, ApprovalStatus decidedStatus, CancellationToken ct)
    {
        if (!_context.Database.IsRelational())
            return;

        var claimed = await _context.ApprovalRequests
            .Where(ar => ar.Id == requestId && (ar.Status == ApprovalStatus.Pending || ar.Status == ApprovalStatus.Escalated))
            .ExecuteUpdateAsync(s => s.SetProperty(ar => ar.Status, decidedStatus), ct);

        if (claimed == 0)
            throw new InvalidOperationException($"Approval request {requestId} is not in Pending status");
    }

    // EscalateOverdueAsync used to store Guid.Empty for "assigned to a role, not a person". Treat that
    // (and null) as unassigned so requests already escalated that way can still be decided.
    private static Guid? AssignedApprover(ApprovalRequest request) =>
        request.ApproverId is { } id && id != Guid.Empty ? id : null;

    private async Task<bool> VerifyApproverAuthorization(ApprovalRequest request, Guid approverId, CancellationToken ct)
    {
        var policy = await _context.ApprovalPolicies
            .Where(p => p.TenantId == request.TenantId && p.IsActive)
            .FirstOrDefaultAsync(ct);

        var allowedRoles = new List<string>();
        if (policy?.ApproverRoles is not null)
            allowedRoles.AddRange(ParseRoles(policy.ApproverRoles));

        // An escalated request is routed to the policy's escalation roles, so those roles may decide it.
        if (request.Status == ApprovalStatus.Escalated && !string.IsNullOrWhiteSpace(request.ApproverRole))
            allowedRoles.AddRange(ParseRoles(request.ApproverRole));

        var roles = allowedRoles.Distinct().ToArray();
        if (roles.Length == 0)
            return false;

        return await _context.Users
            .AnyAsync(u => u.Id == approverId &&
                u.TenantId == request.TenantId &&
                u.UserRoles.Any(ur => roles.Contains(ur.Role!.Name)), ct);
    }

    // ApprovalPolicy.ApproverRoles has shipped in two incompatible shapes:
    // plain comma-separated ("Admin,WorkflowManager") from CreatePolicyAsync,
    // and a JSON-array string ("[\"Admin\",\"WorkflowManager\"]") from the
    // seeded default policy. A plain Split(',') leaves the JSON-seeded
    // policy's role names as '["Admin"' / '"WorkflowManager"]' — quotes and
    // brackets included — so they never match a real role name and every
    // approval is silently unauthorized. Confirmed live: with only the
    // seeded policy active, Approve/Reject 403'd for every user regardless
    // of role. Tolerates both formats without touching the seed data itself.
    private static string[] ParseRoles(string raw) =>
        raw.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries).Select(r => r.Trim('"')).ToArray();

    public async Task<List<PendingApprovalDto>> GetPendingForApproverAsync(
        Guid tenantId, Guid approverId, CancellationToken ct = default)
    {
        return await _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId && ar.ApproverId == approverId
                && (ar.Status == ApprovalStatus.Pending || ar.Status == ApprovalStatus.Escalated))
            .OrderBy(ar => ar.RequestedAt)
            .Select(ar => new PendingApprovalDto
            {
                Id = ar.Id,
                WorkflowInstanceId = ar.WorkflowInstanceId,
                WorkflowId = ar.WorkflowId,
                WorkflowName = ar.Workflow != null ? ar.Workflow.Name : null,
                ApplicationId = ar.Workflow != null ? ar.Workflow.ApplicationId : null,
                ApplicationName = ar.Workflow != null && ar.Workflow.Application != null ? ar.Workflow.Application.Name : null,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data,
                Subject = ar.Subject
            })
            .ToListAsync(ct);
    }

    public async Task<List<PendingApprovalDto>> GetPendingForRoleAsync(
        Guid tenantId, string role, CancellationToken ct = default)
    {
        return await _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId && ar.ApproverRole == role
                && (ar.Status == ApprovalStatus.Pending || ar.Status == ApprovalStatus.Escalated))
            .OrderBy(ar => ar.RequestedAt)
            .Select(ar => new PendingApprovalDto
            {
                Id = ar.Id,
                WorkflowInstanceId = ar.WorkflowInstanceId,
                WorkflowId = ar.WorkflowId,
                WorkflowName = ar.Workflow != null ? ar.Workflow.Name : null,
                ApplicationId = ar.Workflow != null ? ar.Workflow.ApplicationId : null,
                ApplicationName = ar.Workflow != null && ar.Workflow.Application != null ? ar.Workflow.Application.Name : null,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data,
                Subject = ar.Subject
            })
            .ToListAsync(ct);
    }

    public async Task<(List<PendingApprovalDto> Items, int TotalCount)> GetPendingPagedAsync(
        Guid tenantId, Guid? approverId = null, string? role = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.ApprovalRequests
            .Where(ar => ar.TenantId == tenantId
                && (ar.Status == ApprovalStatus.Pending || ar.Status == ApprovalStatus.Escalated));

        // A freshly-created ApprovalRequest (ApprovalStepActivity -> CreateApprovalRequestAsync)
        // never calls AssignApprover — ApproverId/ApproverRole only get populated later, by
        // ApproveAsync/RejectAsync (after the fact), EscalateOverdueAsync, or
        // CreateNextLevelApprovalAsync for a chained level. A strict `== approverId.Value` filter
        // here means a normal first-level pending approval never matches anyone, so /approvals/pending
        // (and the default /approvals?status=Pending) always returned empty regardless of caller —
        // confirmed live: an Approval step really does create a Pending ApprovalRequest, but no
        // "pending" endpoint could ever find it. Broadened to also include not-yet-assigned requests.
        if (approverId.HasValue)
            query = query.Where(ar => ar.ApproverId == null || ar.ApproverId == Guid.Empty || ar.ApproverId == approverId.Value);
        if (!string.IsNullOrEmpty(role))
            query = query.Where(ar => ar.ApproverRole == null || ar.ApproverRole == role);

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
                WorkflowName = ar.Workflow != null ? ar.Workflow.Name : null,
                ApplicationId = ar.Workflow != null ? ar.Workflow.ApplicationId : null,
                ApplicationName = ar.Workflow != null && ar.Workflow.Application != null ? ar.Workflow.Application.Name : null,
                RequesterId = ar.RequesterId,
                RequesterFirstName = ar.Requester.FirstName,
                RequesterLastName = ar.Requester.LastName,
                Status = ar.Status,
                RequestedAt = ar.RequestedAt,
                DueAt = ar.DueAt,
                EscalationLevel = ar.EscalationLevel,
                Data = ar.Data,
                Subject = ar.Subject
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task EscalateOverdueAsync(CancellationToken ct = default)
    {
        // IgnoreQueryFilters: this is a cross-tenant background job — the global tenant filter
        // would silently match nothing here because there is no HttpContext. Soft-delete is
        // re-applied manually since IgnoreQueryFilters also bypasses that filter.
        var overdueIds = await _context.ApprovalRequests
            .IgnoreQueryFilters()
            .Where(ar => !ar.IsDeleted && ar.Status == ApprovalStatus.Pending && ar.DueAt != null && ar.DueAt < DateTime.UtcNow)
            .Select(ar => ar.Id)
            .ToListAsync(ct);

        // ExecuteUpdateAsync (the atomic claim below) is a relational-only feature — the EF Core
        // InMemory provider used by the fast API test suite doesn't support it at all and throws.
        // Real deployments are always relational (Postgres), so this only ever takes the
        // non-atomic fallback path under tests, never in production.
        var canClaimAtomically = _context.Database.IsRelational();

        foreach (var id in overdueIds)
        {
            // Atomic claim: flip Status Pending -> Escalated in one conditional UPDATE, WHERE it's
            // still Pending and still due. Only the replica whose UPDATE actually affects a row owns
            // this request's escalation — same idiom BackgroundJobProcessor uses (Status: Pending ->
            // Processing, WHERE Status = Pending). Without this, two API replicas racing on the same
            // 5-minute sweep window could both load the request, both call Escalate(), and both save
            // — double-incrementing EscalationLevel and double-firing the escalation notification.
            // This was P0-3 from the 2026-09-20 audit, still open until this fix.
            if (canClaimAtomically)
            {
                var claimed = await _context.ApprovalRequests
                    .IgnoreQueryFilters()
                    .Where(ar => ar.Id == id && !ar.IsDeleted && ar.Status == ApprovalStatus.Pending
                        && ar.DueAt != null && ar.DueAt < DateTime.UtcNow)
                    .ExecuteUpdateAsync(s => s.SetProperty(ar => ar.Status, ApprovalStatus.Escalated), ct);

                if (claimed == 0)
                    continue; // another replica claimed it first
            }

            var request = await _context.ApprovalRequests
                .IgnoreQueryFilters()
                .FirstAsync(ar => ar.Id == id, ct);

            // Under the atomic path, Status is already Escalated from the claim UPDATE above —
            // Escalate() re-applies that (a harmless no-op on Status) and does the real per-row
            // work: EscalationLevel++ and ModifiedAt. Under the InMemory fallback, this is the only
            // place Status gets set — same end state either way.
            request.Escalate();

            var policy = await _context.ApprovalPolicies
                .Where(ap => ap.TenantId == request.TenantId && ap.WorkflowType == null && ap.IsActive)
                .FirstOrDefaultAsync(ct);

            if (policy?.EscalationRoles != null && request.ApproverRole != policy.EscalationRoles)
            {
                if (request.ApproverId is { } assigned && assigned != Guid.Empty)
                    request.AssignApprover(assigned, policy.EscalationRoles);
                else
                    request.AssignApproverRole(policy.EscalationRoles);
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogWarning("Escalated approval request {RequestId} to level {Level}",
                request.Id, request.EscalationLevel);
        }
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

        var roleChain = ParseRoles(policy.ApproverRoles);
        var nextLevel = completedRequest.ApprovalLevel + 1;

        if (nextLevel >= roleChain.Length)
            return false;

        var nextRole = roleChain[nextLevel];
        var nextDueAt = policy.EscalationMinutes is int minutes ? DateTime.UtcNow.AddMinutes(minutes) : (DateTime?)null;
        var nextApproval = new ApprovalRequest(
            Guid.NewGuid(), completedRequest.TenantId,
            completedRequest.WorkflowInstanceId, completedRequest.WorkflowId,
            completedRequest.RequesterId, completedRequest.Data, nextDueAt,
            nextLevel, completedRequest.Id, subject: completedRequest.Subject);
        nextApproval.AssignApprover(Guid.Empty, nextRole);

        _context.ApprovalRequests.Add(nextApproval);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created next-level approval {NextId} at level {Level} for role {Role}",
            nextApproval.Id, nextLevel, nextRole);

        await _jobQueue.EnqueueAsync(BackgroundJobTypes.NotifyApprovers,
            new NotifyApproversJobPayload(nextApproval.Id, completedRequest.TenantId, completedRequest.WorkflowId,
                [nextRole], RequesterId: null, completedRequest.Data, EscalationLevel: nextLevel), ct);

        return true;
    }

    // Fire-and-forget from ApproveAsync/RejectAsync — deliberately not awaited there so a slow or
    // failing email/notification send never fails the approval decision itself. That means it runs
    // concurrently with whatever the caller does next on ITS OWN DbContext (e.g. ApprovalsController
    // immediately re-queries the ApprovalRequest to resume the Elsa workflow) — sharing _context
    // here caused a real, reproducible "second operation started on this context instance" crash
    // (same root cause AiFunctionAuditFilter.WriteAuditAsync was fixed for earlier this session:
    // a fire-and-forget side effect entangled with the caller's own unit of work). An isolated scope
    // keeps this notification fully independent of whatever DbContext state the caller is in.
    private async Task NotifyRequesterOfDecisionAsync(ApprovalRequest request, bool approved, string? comments)
    {
        var requesterId = request.RequesterId;
        var workflowId = request.WorkflowId;
        var subject = request.Subject;
        var approvalId = request.Id;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var requester = await context.Users.FindAsync(requesterId);
            var workflow = workflowId is { } wid ? await context.Workflows.FindAsync(wid) : null;
            if (requester is not null)
            {
                // A request with no workflow has no workflow name; its subject says what was decided.
                var what = subject ?? workflow?.Name ?? "Request";

                await emailService.SendApprovalDecisionAsync(
                    requester.Email, requester.FirstName, what,
                    approved, comments, CancellationToken.None);

                await notificationService.SendAsync(requester.Id.ToString(),
                    approved ? "Request Approved" : "Request Rejected",
                    $"Your request for {what} was {(approved ? "approved" : "rejected")}",
                    approved ? "success" : "error", null, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify requester for approval {ApprovalId}", approvalId);
        }
    }
}
