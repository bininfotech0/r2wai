using System.Security.Claims;
using Elsa.Common.Models;
using Elsa.Workflows;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Matches;
using Elsa.Workflows.Runtime.Options;
using Elsa.Workflows.Runtime.Params;
using Elsa.Workflows.Runtime.Parameters;
using Elsa.Workflows.Runtime.Requests;
using Elsa.Workflows.Runtime.Results;
using Elsa.Workflows.State;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Api.Services;
using R2WAI.Api.Workflows;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// P0-5 fallout, more serious than the ChatbotsController one: WorkflowBridge.ContinueDelayedWorkflowAsync
/// — the ONLY caller of which is WorkflowDelayResumeBackgroundService, a background IHostedService with
/// no HttpContext/ambient tenant at all — looked up its WorkflowInstance and Workflow rows through the
/// ambient-filtered DbSet with no .IgnoreQueryFilters(). Under the fail-closed filter, a null ambient
/// tenant now matches zero rows, so this lookup always missed and the method always took its
/// "has no pending delayed resume" early return. Worse than a simple miss: the sweeper's own atomic claim
/// (ExecuteUpdateAsync clearing PendingResumeAt) already ran and committed BEFORE calling in, so the
/// instance's resume trigger is consumed and lost — every workflow with a Delay step would stall
/// permanently and silently the moment this shipped, never picked up again by any future sweep.
/// WorkflowInstanceCompletionNotificationHandler had the identical bug one hop further in (its own
/// WorkflowInstances lookup, reachable from this same no-ambient-tenant call chain when the delayed
/// continuation happens to finish the workflow).
///
/// Program.cs registers IWorkflowBridge as NoOpWorkflowBridge whenever IsEnvironment("Testing") — always
/// true for this test host, regardless of whether Elsa itself is configured — so DI-resolving
/// IWorkflowBridge here would silently test the no-op stand-in, not the real fix. WorkflowBridge is
/// constructed directly instead. IWorkflowRuntime/IWorkflowDefinitionPublisher get throwing stubs: the
/// seeded workflow's only step is the Delay step itself, so RunFromAsync's steps.Count == 0 branch
/// completes the instance directly without ever calling either — if a future change makes it call them,
/// this test fails loudly rather than silently exercising a live Elsa engine this harness can't provide
/// (Program.cs skips AddElsa entirely in "Testing" — a known, already-documented gap from the P0-9 pass
/// earlier this session).
/// </summary>
public class WorkflowDelayResumeTenantFilterRegressionTests : IntegrationTestBase
{
    public WorkflowDelayResumeTenantFilterRegressionTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private sealed class UnusedWorkflowRuntime : IWorkflowRuntime
    {
        private static NotSupportedException NotUsed() => new("Not used by this test's empty-remaining-steps code path.");
        public ValueTask<IWorkflowClient> CreateClientAsync(CancellationToken cancellationToken = default) => throw NotUsed();
        public ValueTask<IWorkflowClient> CreateClientAsync(string workflowInstanceId, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<CanStartWorkflowResult> CanStartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = default) => throw NotUsed();
        public Task<WorkflowExecutionResult> StartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = default) => throw NotUsed();
        public Task<ICollection<WorkflowExecutionResult>> StartWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = default) => throw NotUsed();
        public Task<WorkflowExecutionResult> TryStartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = default) => throw NotUsed();
        public Task<WorkflowExecutionResult> ResumeWorkflowAsync(string workflowInstanceId, ResumeWorkflowRuntimeParams? options = default) => throw NotUsed();
        public Task<ICollection<WorkflowExecutionResult>> ResumeWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = default) => throw NotUsed();
        public Task<TriggerWorkflowsResult> TriggerWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = default) => throw NotUsed();
        public Task<WorkflowExecutionResult> ExecuteWorkflowAsync(WorkflowMatch match, ExecuteWorkflowParams? options = default) => throw NotUsed();
        public Task<CancellationResult> CancelWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<IEnumerable<WorkflowMatch>> FindWorkflowsAsync(WorkflowsFilter filter, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowState> ExportWorkflowStateAsync(string workflowInstanceId, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task ImportWorkflowStateAsync(WorkflowState workflowState, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task UpdateBookmarkAsync(StoredBookmark bookmark, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<long> CountRunningWorkflowsAsync(CountRunningWorkflowsRequest request, CancellationToken cancellationToken = default) => throw NotUsed();
    }

    private sealed class UnusedWorkflowDefinitionPublisher : IWorkflowDefinitionPublisher
    {
        private static NotSupportedException NotUsed() => new("Not used by this test's empty-remaining-steps code path.");
        public WorkflowDefinition New(IActivity? root = default) => throw NotUsed();
        public Task<WorkflowDefinition> NewAsync(IActivity? root = default, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<PublishWorkflowDefinitionResult> PublishAsync(string definitionId, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<PublishWorkflowDefinitionResult> PublishAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowDefinition> RetractAsync(string definitionId, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowDefinition> RetractAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowDefinition> RevertVersionAsync(string definitionId, int version, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowDefinition> GetDraftAsync(string definitionId, VersionOptions versionOptions, CancellationToken cancellationToken = default) => throw NotUsed();
        public Task<WorkflowDefinition> SaveDraftAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => throw NotUsed();
    }

    private IServiceScope SignInWithNoTenantClaim()
    {
        var scope = Factory.Services.CreateScope();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    private static WorkflowBridge CreateRealWorkflowBridge(ApplicationDbContext db) =>
        new(new UnusedWorkflowRuntime(), new UnusedWorkflowDefinitionPublisher(), db,
            new StepActivityFactory([]), NullLogger<WorkflowBridge>.Instance);

    // A single Delay step, last (only) step in the workflow — RunFromAsync's steps.Count == 0 branch for
    // "nothing left to run" completes the instance directly with no Elsa engine call.
    private async Task<(Guid tenantId, Guid instanceId)> SeedDueDelayInstanceAsync()
    {
        using var seedScope = Factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var stepsJson = """[{"Order":0,"Name":"Wait a bit","Type":"delay","Config":null}]""";
        var workflow = new Workflow(Guid.NewGuid(), tenantId, userId, "Delay Regression Workflow", steps: stepsJson);
        db.Workflows.Add(workflow);

        var instance = new R2WAI.Domain.Entities.WorkflowInstance(Guid.NewGuid(), workflow.Id, tenantId, userId);
        instance.ScheduleDelayedResume(DateTime.UtcNow.AddMinutes(-1), stepIndex: 0);
        db.WorkflowInstances.Add(instance);

        var stepExec = new WorkflowStepExecution(Guid.NewGuid(), instance.Id, 0, "Wait a bit", "Delay");
        db.WorkflowStepExecutions.Add(stepExec);

        await db.SaveChangesAsync();
        return (tenantId, instance.Id);
    }

    [Fact]
    public async Task ContinueDelayedWorkflow_WithNoAmbientTenant_StillFindsAndCompletesTheInstance()
    {
        var (_, instanceId) = await SeedDueDelayInstanceAsync();

        // No ambient tenant claim at all — exactly WorkflowDelayResumeBackgroundService's own scope,
        // the only real caller of this method.
        using var scope = SignInWithNoTenantClaim();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workflowBridge = CreateRealWorkflowBridge(db);

        var result = await workflowBridge.ContinueDelayedWorkflowAsync(instanceId, CancellationToken.None);

        // Before the fix this was false — the "has no pending delayed resume" early return, because
        // the WorkflowInstances lookup silently found nothing under the fail-closed ambient filter.
        Assert.True(result);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var instance = await verifyDb.WorkflowInstances.IgnoreQueryFilters().FirstAsync(i => i.Id == instanceId);

        // Proves the full round trip: found despite no ambient tenant, delay cleared, and — since this
        // workflow had nothing left to run after its Delay step — completed.
        Assert.Null(instance.PendingResumeStepIndex);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }
}
