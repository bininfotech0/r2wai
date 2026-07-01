using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Services;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ApprovalsController(
    IApprovalService approvalService,
    IWorkflowBridge workflowBridge,
    ApplicationDbContext dbContext,
    ILogger<ApprovalsController> logger) : ControllerBase
{
    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (claim is null || !Guid.TryParse(claim.Value, out var id))
                throw new UnauthorizedAccessException("Invalid user identity");
            return id;
        }
    }

    private Guid CurrentTenantId
    {
        get
        {
            // JWT issues the claim as "tenant_id" — must match exactly
            var claim = User.FindFirst("tenant_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var tid))
                return tid;
            throw new UnauthorizedAccessException("Tenant context not found");
        }
    }

    private static (int page, int pageSize) ClampPagination(int page, int pageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        if (string.IsNullOrEmpty(status) || status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            var (items, totalCount) = await approvalService.GetPendingPagedAsync(
                CurrentTenantId, CurrentUserId, null, page, pageSize, ct);
            return Ok(new { items, totalCount, page, pageSize });
        }

        if (Enum.TryParse<R2WAI.Domain.Enums.ApprovalStatus>(status, true, out var parsedStatus))
        {
            var requests = await dbContext.ApprovalRequests
                .Where(r => r.TenantId == CurrentTenantId && r.Status == parsedStatus)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var total = await dbContext.ApprovalRequests
                .CountAsync(r => r.TenantId == CurrentTenantId && r.Status == parsedStatus, ct);

            return Ok(new { items = requests, totalCount = total, page, pageSize });
        }

        return BadRequest(new { error = $"Invalid status: {status}" });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var (items, totalCount) = await approvalService.GetPendingPagedAsync(
            CurrentTenantId, CurrentUserId, null, page, pageSize, ct);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("pending/role/{role}")]
    public async Task<IActionResult> GetPendingByRole(string role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var pending = await approvalService.GetPendingForRoleAsync(CurrentTenantId, role, ct);
        var totalCount = pending.Count;
        var paged = pending.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { items = paged, totalCount, page, pageSize });
    }

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies(
        [FromQuery] bool? activeOnly = null,
        CancellationToken ct = default)
    {
        var policies = await approvalService.GetPoliciesAsync(CurrentTenantId, activeOnly, ct);
        return Ok(new { items = policies, totalCount = policies.Count });
    }

    [HttpGet("policies/{id:guid}")]
    public async Task<IActionResult> GetPolicy(Guid id, CancellationToken ct = default)
    {
        var policy = await approvalService.GetPolicyByIdAsync(CurrentTenantId, id, ct);
        return Ok(policy);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreateApprovalPolicyRequest request,
        CancellationToken ct = default)
    {
        var policy = await approvalService.CreatePolicyAsync(CurrentTenantId, request, ct);
        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, policy);
    }

    [HttpPut("policies/{id:guid}")]
    public async Task<IActionResult> UpdatePolicy(Guid id,
        [FromBody] UpdateApprovalPolicyRequest request,
        CancellationToken ct = default)
    {
        var policy = await approvalService.UpdatePolicyAsync(CurrentTenantId, id, request, ct);
        return Ok(policy);
    }

    [HttpDelete("policies/{id:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct = default)
    {
        await approvalService.DeletePolicyAsync(CurrentTenantId, id, ct);
        return NoContent();
    }

    [HttpPost("policies/{id:guid}/toggle")]
    public async Task<IActionResult> TogglePolicy(Guid id, CancellationToken ct = default)
    {
        var policy = await approvalService.TogglePolicyActiveAsync(CurrentTenantId, id, ct);
        return Ok(policy);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalActionRequest request, CancellationToken ct = default)
    {
        var result = await approvalService.ApproveAsync(id, CurrentUserId, request.Comments, ct);

        var approvalRequest = await dbContext.ApprovalRequests
            .Include(ar => ar.WorkflowInstance)
            .FirstOrDefaultAsync(ar => ar.Id == id, ct);

        if (approvalRequest?.WorkflowInstance?.ElsaInstanceId is not null)
        {
            try
            {
                await workflowBridge.ResumeWorkflowAsync(
                    approvalRequest.WorkflowInstance.ElsaInstanceId,
                    id.ToString(),
                    "Approved",
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resume Elsa workflow for approval {RequestId}", id);
            }
        }

        logger.LogInformation("Approval request {RequestId} approved", id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApprovalActionRequest request, CancellationToken ct = default)
    {
        var result = await approvalService.RejectAsync(id, CurrentUserId, request.Comments, ct);

        var approvalRequest = await dbContext.ApprovalRequests
            .Include(ar => ar.WorkflowInstance)
            .FirstOrDefaultAsync(ar => ar.Id == id, ct);

        if (approvalRequest?.WorkflowInstance?.ElsaInstanceId is not null)
        {
            try
            {
                await workflowBridge.ResumeWorkflowAsync(
                    approvalRequest.WorkflowInstance.ElsaInstanceId,
                    id.ToString(),
                    "Rejected",
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resume Elsa workflow for approval {RequestId}", id);
            }
        }

        logger.LogInformation("Approval request {RequestId} rejected", id);
        return Ok(result);
    }
}

public sealed record ApprovalActionRequest(string? Comments = null);
