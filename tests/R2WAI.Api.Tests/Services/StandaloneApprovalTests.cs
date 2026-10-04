using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Tests.Services;

/// <summary>
/// An approval is a generic authorisation gate and must not need a workflow to exist. Every
/// ApprovalRequest used to carry non-null WorkflowInstanceId / WorkflowId foreign keys, so nothing could
/// ask a human for a decision without a workflow behind it — which blocks removing the workflow runtime.
/// </summary>
public class StandaloneApprovalTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // "deptadmin@r2wai.io" — seeded, Admin role, same tenant, but a different person from AdminUserId.
    // Approving/rejecting needs a real distinct approver now that a requester can't decide their own
    // request (see ApprovalService.ApproveAsync/RejectAsync's separation-of-duties check).
    private static readonly Guid ApproverUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly R2WAIWebApplicationFactory _factory;

    public StandaloneApprovalTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    // Synchronous by design — see the class-level note above CreateStandaloneRequestDataAsync for why.
    // Matches ToolGovernanceFilterTests.SignIn exactly.
    private IServiceScope SignedInScope()
    {
        var scope = _factory.Services.CreateScope();
        var claims = new List<Claim> { new("tenant_id", TenantId.ToString()), new(ClaimTypes.NameIdentifier, AdminUserId.ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    // P0-5's fail-closed tenant filter (same session): IHttpContextAccessor.HttpContext is set via
    // AsyncLocal, which flows INTO an awaited async method but a mutation made *inside* one is
    // invisible to the caller once that method returns (a real, verified .NET behavior, not specific
    // to this codebase) — so setting it inside an async helper the test then awaits doesn't work; it
    // must be set synchronously, in the same frame that will later call ApproveAsync/RejectAsync/etc.
    // This helper only *reads* the ambient context (already set by SignedInScope, called directly in
    // each test below), so it's safe for it to be async — it never mutates the accessor itself.
    private static async Task<Guid> CreateStandaloneRequestDataAsync(IApprovalService service)
    {
        await service.CreatePolicyAsync(TenantId, new CreateApprovalPolicyRequest(
            Name: "Standalone test " + Guid.NewGuid(), Description: null, WorkflowType: null,
            ApproverRoles: "Admin", MinApprovers: 1, EscalationMinutes: null, EscalationRoles: null));

        return await service.CreateApprovalRequestAsync(
            TenantId, workflowInstanceId: null, workflowId: null, requesterId: AdminUserId,
            data: "{\"supplier\":\"ABC Industries\"}", subject: "Submit supplier ABC Industries");
    }

    [Fact]
    public async Task A_request_with_no_workflow_is_listed_as_pending_with_its_subject()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var requestId = await CreateStandaloneRequestDataAsync(service);

        var (items, _) = await service.GetPendingPagedAsync(TenantId, approverId: AdminUserId);

        var item = Assert.Single(items, i => i.Id == requestId);
        Assert.Equal("Submit supplier ABC Industries", item.Subject);
        Assert.Null(item.WorkflowId);
        Assert.Null(item.WorkflowInstanceId);
        Assert.Null(item.WorkflowName);
    }

    [Fact]
    public async Task A_request_with_no_workflow_can_be_approved()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateStandaloneRequestDataAsync(service);

        var result = await service.ApproveAsync(requestId, ApproverUserId, "approved");

        Assert.True(result.IsApproved);
        Assert.Null(result.WorkflowInstanceId);
        var stored = await context.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == requestId);
        Assert.Equal(ApprovalStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task A_request_with_no_workflow_can_be_rejected()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateStandaloneRequestDataAsync(service);

        var result = await service.RejectAsync(requestId, ApproverUserId, "rejected");

        Assert.False(result.IsApproved);
        Assert.Null(result.WorkflowInstanceId);
        var stored = await context.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == requestId);
        Assert.Equal(ApprovalStatus.Rejected, stored.Status);
    }

    [Fact]
    public async Task The_requester_cannot_approve_their_own_request()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var requestId = await CreateStandaloneRequestDataAsync(service);

        // AdminUserId is the requester (see CreateStandaloneRequestDataAsync) — even though it holds
        // the Admin role the policy allows, separation of duties still blocks it from deciding its own
        // request.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(requestId, AdminUserId, "self-approved"));
    }

    [Fact]
    public async Task The_requester_cannot_reject_their_own_request_either()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var requestId = await CreateStandaloneRequestDataAsync(service);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RejectAsync(requestId, AdminUserId, "self-rejected"));
    }

    [Fact]
    public async Task The_approver_notification_job_tolerates_a_request_with_no_workflow()
    {
        await _factory.EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>()
            .Single(h => h.JobType == BackgroundJobTypes.NotifyApprovers);
        var payload = new NotifyApproversJobPayload(
            Guid.NewGuid(), TenantId, WorkflowId: null, Roles: ["Admin"], RequesterId: AdminUserId,
            Data: null, EscalationLevel: null);

        var ex = await Record.ExceptionAsync(() =>
            handler.HandleAsync(JsonSerializer.Serialize(payload), CancellationToken.None));

        Assert.Null(ex);
    }
}
