using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Tests.Services;

/// <summary>
/// Audit finding P0-10: the overdue sweep flips a request to Escalated, but ApproveAsync/RejectAsync only
/// accepted Pending, and the sweep also assigned the request to Guid.Empty when nobody was assigned —
/// so an escalated approval could never be decided by anyone.
/// </summary>
public class ApprovalEscalationTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // "deptadmin@r2wai.io" — seeded, Admin role, same tenant, but a different person from AdminUserId.
    // Deciding needs a real distinct approver now that a requester can't decide their own request (see
    // ApprovalService.ApproveAsync/RejectAsync's separation-of-duties check).
    private static readonly Guid ApproverUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly R2WAIWebApplicationFactory _factory;

    public ApprovalEscalationTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    // Synchronous by design — see the doc comment on CreateEscalatedRequestDataAsync for why. Matches
    // ToolGovernanceFilterTests.SignIn exactly.
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
    // invisible to the caller once that method returns — so it must be set synchronously, in the same
    // frame that will later call ApproveAsync/RejectAsync/etc. (SignedInScope, called directly in each
    // test below). This helper only *reads* the ambient context, never mutates it, so it's safe to be
    // async.
    private static async Task<Guid> CreateEscalatedRequestDataAsync(IApprovalService service, ApplicationDbContext context)
    {
        // Both routes matter: ApproverRoles authorises the decision, EscalationRoles is what the sweep
        // assigns the request to (the path that used to write Guid.Empty into ApproverId).
        await service.CreatePolicyAsync(TenantId, new CreateApprovalPolicyRequest(
            Name: "Escalation test " + Guid.NewGuid(), Description: null, WorkflowType: null,
            ApproverRoles: "Admin", MinApprovers: 1, EscalationMinutes: 30, EscalationRoles: "Admin"));

        // Real workflow + requester rows: the paged pending list projects ar.Workflow / ar.Requester, and
        // the in-memory provider drops rows whose required navigations don't exist.
        var workflow = new Workflow(Guid.NewGuid(), TenantId, AdminUserId, "Escalation test workflow");
        context.Workflows.Add(workflow);
        var overdue = new ApprovalRequest(Guid.NewGuid(), TenantId, Guid.NewGuid(), workflow.Id,
            requesterId: AdminUserId, data: "supplier update", dueAt: DateTime.UtcNow.AddMinutes(-10));
        context.ApprovalRequests.Add(overdue);
        await context.SaveChangesAsync();

        await service.EscalateOverdueAsync();

        var escalated = await context.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == overdue.Id);
        Assert.Equal(ApprovalStatus.Escalated, escalated.Status);
        return overdue.Id;
    }

    [Fact]
    public async Task Escalated_request_can_still_be_approved()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateEscalatedRequestDataAsync(service, context);

        var result = await service.ApproveAsync(requestId, ApproverUserId, "approved after escalation");

        Assert.True(result.IsApproved);
        var stored = await context.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == requestId);
        Assert.Equal(ApprovalStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task Escalated_request_can_still_be_rejected()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateEscalatedRequestDataAsync(service, context);

        var result = await service.RejectAsync(requestId, ApproverUserId, "rejected after escalation");

        Assert.False(result.IsApproved);
        var stored = await context.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == requestId);
        Assert.Equal(ApprovalStatus.Rejected, stored.Status);
    }

    [Fact]
    public async Task Escalated_request_still_shows_up_as_needing_a_decision()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateEscalatedRequestDataAsync(service, context);

        var (items, _) = await service.GetPendingPagedAsync(TenantId);

        Assert.Contains(items, i => i.Id == requestId);
    }

    [Fact]
    public async Task A_decided_request_cannot_be_decided_again()
    {
        await _factory.EnsureSeededAsync();
        using var scope = SignedInScope();
        var service = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requestId = await CreateEscalatedRequestDataAsync(service, context);
        await service.ApproveAsync(requestId, ApproverUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(requestId, ApproverUserId));
    }
}
