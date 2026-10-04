using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Api.Hubs;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// Audit findings P0-5 / P1-7. StatusHub.SubscribeToWorkflow added ANY authenticated caller to a run's
/// group (cross-tenant listening), and ChatHub only checked the tenant when joining a conversation (any
/// user could join another user's conversation).
/// </summary>
public class HubAuthorizationTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly R2WAIWebApplicationFactory _factory;

    public HubAuthorizationTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    private sealed class FakeGroups : IGroupManager
    {
        public List<string> Joined { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Joined.Add(groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Joined.Remove(groupName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCallerContext(ClaimsPrincipal user, string userIdentifier) : HubCallerContext
    {
        public override string ConnectionId => "test-connection";
        public override string? UserIdentifier => userIdentifier;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private static FakeCallerContext Caller(Guid tenantId, Guid userId) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", tenantId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")),
            userId.ToString());

    private async Task<(IServiceScope Scope, ApplicationDbContext Context)> NewContextAsync()
    {
        var scope = _factory.Services.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    [Fact]
    public async Task StatusHub_refuses_a_subscription_to_another_tenants_run()
    {
        var (scope, context) = await NewContextAsync();
        using var _scope = scope;
        var run = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), TenantA, Guid.NewGuid(), data: null);
        context.WorkflowInstances.Add(run);
        await context.SaveChangesAsync();
        var groups = new FakeGroups();
        var hub = new StatusHub(NullLogger<StatusHub>.Instance, context) { Context = Caller(TenantB, Guid.NewGuid()), Groups = groups };

        await hub.SubscribeToWorkflow(run.Id.ToString());

        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task StatusHub_lets_a_caller_from_the_runs_own_tenant_subscribe()
    {
        var (scope, context) = await NewContextAsync();
        using var _scope = scope;
        var run = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), TenantA, Guid.NewGuid(), data: null);
        context.WorkflowInstances.Add(run);
        await context.SaveChangesAsync();
        var groups = new FakeGroups();
        var hub = new StatusHub(NullLogger<StatusHub>.Instance, context) { Context = Caller(TenantA, Guid.NewGuid()), Groups = groups };

        await hub.SubscribeToWorkflow(run.Id.ToString());

        Assert.Equal([$"workflow_{run.Id}"], groups.Joined);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task StatusHub_ignores_a_malformed_run_id(string runId)
    {
        var (scope, context) = await NewContextAsync();
        using var _scope = scope;
        var groups = new FakeGroups();
        var hub = new StatusHub(NullLogger<StatusHub>.Instance, context) { Context = Caller(TenantA, Guid.NewGuid()), Groups = groups };

        await hub.SubscribeToWorkflow(runId);

        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task ChatHub_refuses_another_users_conversation_in_the_same_tenant()
    {
        var (scope, context) = await NewContextAsync();
        using var _scope = scope;
        var owner = Guid.NewGuid();
        var conversation = new Conversation(Guid.NewGuid(), TenantA, owner, "private");
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        var groups = new FakeGroups();
        var hub = new ChatHub(context, NullLogger<ChatHub>.Instance) { Context = Caller(TenantA, Guid.NewGuid()), Groups = groups };

        await hub.JoinConversation(conversation.Id.ToString());

        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task ChatHub_lets_the_owner_join_their_own_conversation()
    {
        var (scope, context) = await NewContextAsync();
        using var _scope = scope;
        var owner = Guid.NewGuid();
        var conversation = new Conversation(Guid.NewGuid(), TenantA, owner, "mine");
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        var groups = new FakeGroups();
        var hub = new ChatHub(context, NullLogger<ChatHub>.Instance) { Context = Caller(TenantA, owner), Groups = groups };

        await hub.JoinConversation(conversation.Id.ToString());

        Assert.Equal([$"conversation_{conversation.Id}"], groups.Joined);
    }
}
