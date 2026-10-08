using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.Services.ToolFramework;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers DeferredToolCallExecutor — the resume half of the deferred-tool-call approval flow (the
/// create half is DeferredToolCallPayload + AiFunctionAuditFilter's DenyApprovalRequired branch,
/// proven end to end against a real DB in ToolGovernanceFilterTests). Uses the same fake
/// ITool/IToolRegistry as DynamicToolExecutorTests — no real HTTP call happens.
/// </summary>
public class DeferredToolCallExecutorTests
{
    private sealed class FakeTool : ITool
    {
        public string Name => "HttpTool";
        public string Description => "fake";
        public ToolContext? LastContext { get; private set; }
        public ToolResult Result { get; set; } = new() { Success = true, Data = "ok" };
        public Exception? Throws { get; set; }

        public Task<ToolResult> ExecuteAsync(ToolContext context)
        {
            LastContext = context;
            if (Throws is not null) throw Throws;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
        public void Register(ITool tool) => _tools[tool.Name] = tool;
        public ITool? Get(string name) => _tools.GetValueOrDefault(name);
        public IEnumerable<ITool> GetAll() => _tools.Values;
    }

    private sealed class FakeToolDefinitionRepository : R2WAI.Domain.Interfaces.IRepository<ToolDefinition>
    {
        private readonly List<ToolDefinition> _rows;
        public FakeToolDefinitionRepository(params ToolDefinition[] rows) => _rows = rows.ToList();

        public Task<ToolDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(t => t.Id == id));
        public Task<ToolDefinition?> GetByIdAsync(Guid id, System.Linq.Expressions.Expression<Func<ToolDefinition, object>> include, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<ToolDefinition?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);
        public Task<IReadOnlyList<ToolDefinition>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ToolDefinition>)_rows);
        public Task<IReadOnlyList<ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ToolDefinition>)_rows.AsQueryable().Where(predicate).ToList());
        public Task<IReadOnlyList<ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<ToolDefinition, bool>> predicate, string includePath, CancellationToken ct = default) =>
            FindAsync(predicate, ct);
        public Task<ToolDefinition?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<ToolDefinition> AddAsync(ToolDefinition entity, CancellationToken ct = default) { _rows.Add(entity); return Task.FromResult(entity); }
        public void Update(ToolDefinition entity) { }
        public void Delete(ToolDefinition entity) => _rows.Remove(entity);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.Any(t => t.Id == id));
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Count(predicate));
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    // Audit rows and ledger rows the executor wrote through its isolated scope.
    private readonly List<AuditLog> _audits = [];
    private readonly List<ToolExecution> _executions = [];

    private IServiceScopeFactory CreateAuditScopeFactory()
    {
        var auditRepo = new Mock<R2WAI.Domain.Interfaces.IRepository<AuditLog>>();
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => _audits.Add(a))
            .ReturnsAsync((AuditLog a, CancellationToken _) => a);
        var executionRepo = new Mock<R2WAI.Domain.Interfaces.IRepository<ToolExecution>>();
        executionRepo.Setup(r => r.AddAsync(It.IsAny<ToolExecution>(), It.IsAny<CancellationToken>()))
            .Callback<ToolExecution, CancellationToken>((e, _) => _executions.Add(e))
            .ReturnsAsync((ToolExecution e, CancellationToken _) => e);
        executionRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ToolExecution, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<ToolExecution, bool>> predicate, CancellationToken _) =>
                _executions.AsQueryable().FirstOrDefault(predicate));
        executionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _executions.FirstOrDefault(e => e.Id == id));
        var unitOfWork = new Mock<R2WAI.Domain.Interfaces.IUnitOfWork>();
        return new ServiceCollection()
            .AddSingleton(executionRepo.Object)
            .AddSingleton(auditRepo.Object)
            .AddSingleton(unitOfWork.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
    }

    private DeferredToolCallExecutor CreateSut(FakeTool fakeTool, params ToolDefinition[] toolDefs)
    {
        var registry = new FakeRegistry();
        registry.Register(fakeTool);
        var dynamicExecutor = new DynamicToolExecutor(registry, new NoOpEncryptionService(), NullLogger<DynamicToolExecutor>.Instance);
        var mcpClient = new McpClientAdapter(NullLogger<McpClientAdapter>.Instance);
        var mcpExecutor = new McpDynamicToolExecutor(mcpClient, new NoOpEncryptionService(), NullLogger<McpDynamicToolExecutor>.Instance);
        return new DeferredToolCallExecutor(
            new FakeToolDefinitionRepository(toolDefs), dynamicExecutor, mcpExecutor, CreateAuditScopeFactory(),
            NullLogger<DeferredToolCallExecutor>.Instance);
    }

    private sealed class NoOpEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
    }

    private static ApprovalRequest CreateStandaloneRequest(Guid tenantId, string? data) =>
        new(Guid.NewGuid(), tenantId, workflowInstanceId: null, workflowId: null, requesterId: Guid.NewGuid(), data: data);

    [Fact]
    public async Task TryExecuteAsync_RequestWithNoPayload_ReturnsNull_NeverTouchesTheToolRegistry()
    {
        var fakeTool = new FakeTool();
        var sut = CreateSut(fakeTool);
        var request = CreateStandaloneRequest(Guid.NewGuid(), data: null);

        var result = await sut.TryExecuteAsync(request);

        Assert.Null(result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task TryExecuteAsync_WorkflowBoundApprovalsFreeTextData_ReturnsNull()
    {
        // A workflow step's own free-text Data must never be misread as a tool call to replay.
        var fakeTool = new FakeTool();
        var sut = CreateSut(fakeTool);
        var request = CreateStandaloneRequest(Guid.NewGuid(), data: "supplier update");

        var result = await sut.TryExecuteAsync(request);

        Assert.Null(result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task TryExecuteAsync_ValidPayload_ButToolNoLongerExists_ReturnsGuardMessage()
    {
        var fakeTool = new FakeTool();
        var sut = CreateSut(fakeTool); // no ToolDefinition rows at all
        var tenantId = Guid.NewGuid();
        var payload = new DeferredToolCallPayload(Guid.NewGuid(), "{}");
        var request = CreateStandaloneRequest(tenantId, payload.ToJson());

        var result = await sut.TryExecuteAsync(request);

        Assert.Equal("The tool this approval was for no longer exists.", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task TryExecuteAsync_ValidPayload_ButToolBelongsToADifferentTenant_ReturnsGuardMessage()
    {
        var fakeTool = new FakeTool();
        var otherTenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), otherTenantId, "Get Status", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);

        var payload = new DeferredToolCallPayload(toolDef.Id, null);
        var request = CreateStandaloneRequest(Guid.NewGuid(), payload.ToJson()); // different tenant

        var result = await sut.TryExecuteAsync(request);

        Assert.Equal("The tool this approval was for no longer exists.", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task TryExecuteAsync_ValidPayload_DispatchesTheExactToolAndInputCapturedAtPauseTime()
    {
        var fakeTool = new FakeTool { Result = new ToolResult { Success = true, Data = "{\"orderId\":42}" } };
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Create Order", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);

        var payload = new DeferredToolCallPayload(toolDef.Id, "{\"sku\":\"ABC\"}");
        var request = CreateStandaloneRequest(tenantId, payload.ToJson());

        var result = await sut.TryExecuteAsync(request);

        Assert.Equal("{\"orderId\":42}", result);
        Assert.NotNull(fakeTool.LastContext);
        Assert.Equal("{\"sku\":\"ABC\"}", fakeTool.LastContext!.Parameters["body"]);
        Assert.Equal("https://api.example.com", fakeTool.LastContext.Parameters["baseUrl"]);
    }

    [Fact]
    public async Task TryExecuteAsync_ValidPayload_ForAnMcpTool_RoutesToTheMcpExecutor_NotTheHttpOne()
    {
        // No real MCP server needed: a blocked (internal) endpoint makes McpDynamicToolExecutor return
        // its own guard message deterministically and without any network call — proving this replay
        // path actually dispatches Mcp-type tools through McpDynamicToolExecutor (not DynamicToolExecutor,
        // which would instead report "is not linked to a registered API").
        var fakeTool = new FakeTool();
        var tenantId = Guid.NewGuid();
        var connection = new McpServerConnection(Guid.NewGuid(), tenantId, "Internal MCP", "http://169.254.169.254/mcp");
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Get Status", ToolType.Mcp, "desc");
        toolDef.LinkMcpServer(connection.Id, "get_status");
        typeof(ToolDefinition).GetProperty(nameof(ToolDefinition.McpServerConnection))!.SetValue(toolDef, connection);

        var sut = CreateSut(fakeTool, toolDef);
        var payload = new DeferredToolCallPayload(toolDef.Id, null);
        var request = CreateStandaloneRequest(tenantId, payload.ToJson());

        var result = await sut.TryExecuteAsync(request);

        Assert.Contains("not allowed", result);
        Assert.Null(fakeTool.LastContext); // never reached the HTTP-tool dispatch path
    }

    [Fact]
    public async Task TryExecuteAsync_ApprovedCall_WritesAnExecutedAuditRow_LinkedToItsApproval()
    {
        var fakeTool = new FakeTool();
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Create Order", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolDef.Id, "{}").ToJson());

        await sut.TryExecuteAsync(request);

        var audit = Assert.Single(_audits);
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(toolDef.Id.ToString(), audit.EntityId);
        Assert.Equal(request.RequesterId, audit.UserId);
        Assert.Contains("\"status\":\"executed\"", audit.Metadata);
        Assert.Contains(request.Id.ToString(), audit.Metadata);
    }

    [Fact]
    public async Task TryExecuteAsync_ApprovedCallThatThrows_RecordsTheFailure_InsteadOfLosingIt()
    {
        var fakeTool = new FakeTool { Throws = new HttpRequestException("upstream 502") };
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Create Order", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolDef.Id, "{}").ToJson());

        var result = await sut.TryExecuteAsync(request);

        Assert.Equal("Execution failed: upstream 502", result); // lands on the approval's comments
        var audit = Assert.Single(_audits);
        Assert.Contains("\"status\":\"failed\"", audit.Metadata);
        Assert.Contains("upstream 502", audit.Metadata);
    }

    [Fact]
    public async Task TryExecuteAsync_ToolWithAuditDisabled_RunsWithoutAnAuditRow()
    {
        var fakeTool = new FakeTool();
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Get Status", ToolType.Http, "desc", "https://api.example.com");
        toolDef.ConfigureGovernance("Low", requiredRole: null, confirmationRequired: false, approvalRequired: true, auditRequired: false);
        var sut = CreateSut(fakeTool, toolDef);
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolDef.Id, null).ToJson());

        await sut.TryExecuteAsync(request);

        Assert.NotNull(fakeTool.LastContext);
        Assert.Empty(_audits);
    }

    [Fact]
    public async Task TryExecuteAsync_ApprovedPausedCall_MovesItsLedgerRowFromAwaitingApprovalToSucceeded()
    {
        var fakeTool = new FakeTool();
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Create Order", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolDef.Id, "{}").ToJson());
        _executions.Add(ToolExecution.AwaitingApproval(tenantId, request.RequesterId, toolDef.Id, "Tools", "Create Order", request.Id));

        await sut.TryExecuteAsync(request);

        var execution = Assert.Single(_executions); // the paused row was continued, not duplicated
        Assert.Equal(ToolExecutionStatus.Succeeded, execution.Status);
        Assert.NotNull(execution.DurationMs);
    }

    [Fact]
    public async Task TryExecuteAsync_ApprovedCallThatThrows_LeavesAFailedLedgerRow()
    {
        var fakeTool = new FakeTool { Throws = new HttpRequestException("upstream 502") };
        var tenantId = Guid.NewGuid();
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Create Order", ToolType.Http, "desc", "https://api.example.com");
        var sut = CreateSut(fakeTool, toolDef);
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolDef.Id, "{}").ToJson());

        await sut.TryExecuteAsync(request); // no paused row: a request from before the ledger existed

        var execution = Assert.Single(_executions);
        Assert.Equal(ToolExecutionStatus.Failed, execution.Status);
        Assert.Equal(request.Id, execution.ApprovalRequestId);
        Assert.Equal("upstream 502", execution.Error);
    }

    [Fact]
    public async Task RecordRejectionAsync_ClosesThePausedRowAsDenied_AndIgnoresNonToolApprovals()
    {
        var tenantId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var sut = CreateSut(new FakeTool());
        var request = CreateStandaloneRequest(tenantId, new DeferredToolCallPayload(toolId, "{}").ToJson());
        _executions.Add(ToolExecution.AwaitingApproval(tenantId, request.RequesterId, toolId, "Tools", "Create Order", request.Id));

        await sut.RecordRejectionAsync(request);
        await sut.RecordRejectionAsync(CreateStandaloneRequest(tenantId, data: "supplier update"));

        var execution = Assert.Single(_executions);
        Assert.Equal(ToolExecutionStatus.Denied, execution.Status);
        Assert.Equal("confirmation rejected", execution.DenialReason);
    }
}
