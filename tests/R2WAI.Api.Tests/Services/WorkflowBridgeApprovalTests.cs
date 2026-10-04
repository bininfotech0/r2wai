using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Api.Services;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Services;

/// <summary>
/// Audit finding P0-1: a rejected approval used to resume the run and execute every remaining step,
/// because WorkflowBridge.ResumeWorkflowAsync ignored the decision it was handed. The rejected and
/// approved-as-last-step branches never touch Elsa, so the bridge is built here with null Elsa
/// dependencies and exercised directly against the in-memory database.
/// </summary>
public class WorkflowBridgeApprovalTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private readonly R2WAIWebApplicationFactory _factory;

    public WorkflowBridgeApprovalTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    private static WorkflowBridge NewBridge(ApplicationDbContext context) =>
        new(workflowRuntime: null!, publisher: null!, context, stepActivityFactory: null!, NullLogger<WorkflowBridge>.Instance);

    // Synchronous by design — see the doc comment on SeedRunSuspendedAtApprovalAsync for why. Matches
    // ToolGovernanceFilterTests.SignIn exactly. Generates a fresh tenant/user here (not inside the
    // async seed helper) so the ambient claim and the seeded rows' TenantId always agree.
    private static (IServiceScope Scope, Guid TenantId, Guid UserId) SignedInScope(IServiceProvider services)
    {
        var scope = services.CreateScope();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new("tenant_id", tenantId.ToString()), new(ClaimTypes.NameIdentifier, userId.ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return (scope, tenantId, userId);
    }

    // P0-5's fail-closed tenant filter (same session): IHttpContextAccessor.HttpContext is set via
    // AsyncLocal, which flows INTO an awaited async method but a mutation made *inside* one is
    // invisible to the caller once that method returns — so it must be set synchronously, in the same
    // frame that will later call WorkflowBridge.ResumeWorkflowAsync (SignedInScope, called directly in
    // each test below). This helper only *reads* the ambient context, never mutates it.
    private static async Task<(ApplicationDbContext Context, Guid InstanceId, string ElsaInstanceId)> SeedRunSuspendedAtApprovalAsync(
        IServiceScope scope, Guid tenantId, Guid userId, int stepCount)
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var steps = Enumerable.Range(0, stepCount)
            .Select(i => new
            {
                order = i,
                name = i == 1 ? "Manager approval" : $"Step {i}",
                type = i == 1 ? "approval" : "log"
            })
            .ToArray();

        var workflow = new Workflow(Guid.NewGuid(), tenantId, userId, "Supplier update", steps: JsonSerializer.Serialize(steps));
        var instance = new WorkflowInstance(Guid.NewGuid(), workflow.Id, tenantId, userId, data: null);
        var elsaInstanceId = $"elsa-{Guid.NewGuid():N}";
        instance.SetElsaInstanceId(elsaInstanceId);

        context.Workflows.Add(workflow);
        context.WorkflowInstances.Add(instance);

        for (var i = 0; i < stepCount; i++)
        {
            var execution = new WorkflowStepExecution(Guid.NewGuid(), instance.Id, i, steps[i].name, steps[i].type);
            if (i == 0) { execution.Start(); execution.Complete(); }
            else if (i == 1) execution.Start(); // suspended at the approval step
            context.WorkflowStepExecutions.Add(execution);
        }

        await context.SaveChangesAsync();
        return (context, instance.Id, elsaInstanceId);
    }

    [Fact]
    public async Task Rejected_approval_cancels_the_run_and_never_executes_the_remaining_steps()
    {
        var (scope, tenantId, userId) = SignedInScope(_factory.Services);
        using var _scope = scope;
        var (context, instanceId, elsaInstanceId) = await SeedRunSuspendedAtApprovalAsync(scope, tenantId, userId, stepCount: 4);

        await NewBridge(context).ResumeWorkflowAsync(elsaInstanceId, Guid.NewGuid().ToString(), "Rejected", CancellationToken.None);

        var instance = await context.WorkflowInstances.AsNoTracking().SingleAsync(i => i.Id == instanceId);
        var steps = await context.WorkflowStepExecutions.AsNoTracking()
            .Where(s => s.WorkflowInstanceId == instanceId).OrderBy(s => s.StepIndex).ToListAsync();

        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
        Assert.Equal(WorkflowStepStatus.Completed, steps[1].Status);
        Assert.Contains("Rejected", steps[1].Output);
        Assert.Equal(WorkflowStepStatus.Skipped, steps[2].Status);
        Assert.Equal(WorkflowStepStatus.Skipped, steps[3].Status);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    [InlineData("anything-but-approved")]
    public async Task Any_decision_other_than_approved_also_stops_the_run(string decision)
    {
        var (scope, tenantId, userId) = SignedInScope(_factory.Services);
        using var _scope = scope;
        var (context, instanceId, elsaInstanceId) = await SeedRunSuspendedAtApprovalAsync(scope, tenantId, userId, stepCount: 3);

        await NewBridge(context).ResumeWorkflowAsync(elsaInstanceId, Guid.NewGuid().ToString(), decision, CancellationToken.None);

        var instance = await context.WorkflowInstances.AsNoTracking().SingleAsync(i => i.Id == instanceId);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
    }

    [Fact]
    public async Task Approval_on_the_last_step_still_completes_the_run()
    {
        var (scope, tenantId, userId) = SignedInScope(_factory.Services);
        using var _scope = scope;
        // Steps 0 (done) and 1 (approval, suspended) only, so an approval leaves nothing to run and the
        // bridge completes the instance without ever calling Elsa.
        var (context, instanceId, elsaInstanceId) = await SeedRunSuspendedAtApprovalAsync(scope, tenantId, userId, stepCount: 2);

        await NewBridge(context).ResumeWorkflowAsync(elsaInstanceId, Guid.NewGuid().ToString(), "Approved", CancellationToken.None);

        var instance = await context.WorkflowInstances.AsNoTracking().SingleAsync(i => i.Id == instanceId);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }
}
