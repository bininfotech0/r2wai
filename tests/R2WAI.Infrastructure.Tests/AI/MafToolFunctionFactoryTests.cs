using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers MafToolFunctionFactory's filtering (implementation plan Phase 4's explicit descope: "no
/// write-capable tools on MAF yet"). Never actually invokes a built AIFunction — that would need a
/// real IToolGateway/DynamicToolExecutor — this only checks which ToolDefinition rows make it into
/// the returned tool list at all, by name.
/// </summary>
public class MafToolFunctionFactoryTests
{
    private sealed class FakeToolDefinitionRepository : IRepository<ToolDefinition>
    {
        private readonly List<ToolDefinition> _rows;
        public FakeToolDefinitionRepository(params ToolDefinition[] rows) => _rows = rows.ToList();

        public Task<ToolDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.FirstOrDefault(t => t.Id == id));
        public Task<ToolDefinition?> GetByIdAsync(Guid id, Expression<Func<ToolDefinition, object>> include, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<ToolDefinition?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<ToolDefinition>> GetAllAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<ToolDefinition>)_rows);
        public Task<IReadOnlyList<ToolDefinition>> FindAsync(Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<ToolDefinition>)_rows.AsQueryable().Where(predicate).ToList());
        public Task<IReadOnlyList<ToolDefinition>> FindAsync(Expression<Func<ToolDefinition, bool>> predicate, string includePath, CancellationToken ct = default) => FindAsync(predicate, ct);
        public Task<ToolDefinition?> FirstOrDefaultAsync(Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_rows.AsQueryable().Where(predicate).FirstOrDefault());
        public Task<ToolDefinition> AddAsync(ToolDefinition entity, CancellationToken ct = default) { _rows.Add(entity); return Task.FromResult(entity); }
        public void Update(ToolDefinition entity) { }
        public void Delete(ToolDefinition entity) => _rows.Remove(entity);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_rows.Any(t => t.Id == id));
        public Task<int> CountAsync(Expression<Func<ToolDefinition, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_rows.AsQueryable().Count(predicate));
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? TenantId { get; init; } = Guid.NewGuid();
        public string[] Roles => [];
        public bool IsAuthenticated => true;
        public string? IpAddress => null;
        public string? CorrelationId => null;
    }

    private static ToolDefinition CreateHttpToolDef(Guid tenantId, string name, string? httpMethod, string endpointUrl = "https://api.example.com")
    {
        var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, name, ToolType.Http, "desc", endpointUrl);
        toolDef.LinkApi(null, httpMethod, "status");
        return toolDef;
    }

    private static MafToolFunctionFactory CreateSut(Guid tenantId, params ToolDefinition[] toolDefs) =>
        new(new FakeToolDefinitionRepository(toolDefs), null!, null!,
            new FakeCurrentUserService { TenantId = tenantId }, NullLogger<MafToolFunctionFactory>.Instance);

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData(null)]
    public async Task BuildToolsAsync_ReadOnlyHttpMethod_IsIncluded(string? method)
    {
        var tenantId = Guid.NewGuid();
        var toolDef = CreateHttpToolDef(tenantId, "Get Status", method);
        var sut = CreateSut(tenantId, toolDef);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Contains(tools, t => t.Name == "Get_Status");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task BuildToolsAsync_WriteHttpMethod_IsExcluded(string method)
    {
        var tenantId = Guid.NewGuid();
        var toolDef = CreateHttpToolDef(tenantId, "Delete Order", method);
        var sut = CreateSut(tenantId, toolDef);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task BuildToolsAsync_McpTool_IsExcludedOutright_EvenWhenActive()
    {
        var tenantId = Guid.NewGuid();
        var mcpToolDef = new ToolDefinition(Guid.NewGuid(), tenantId, "Get Weather", ToolType.Mcp, "desc");
        mcpToolDef.LinkMcpServer(Guid.NewGuid(), "get_weather");
        var sut = CreateSut(tenantId, mcpToolDef);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task BuildToolsAsync_InactiveTool_IsExcluded()
    {
        var tenantId = Guid.NewGuid();
        var toolDef = CreateHttpToolDef(tenantId, "Get Status", "GET");
        toolDef.Deactivate();
        var sut = CreateSut(tenantId, toolDef);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task BuildToolsAsync_EnabledToolIdsScopesToThatSubset()
    {
        var tenantId = Guid.NewGuid();
        var included = CreateHttpToolDef(tenantId, "Get Status", "GET");
        var excluded = CreateHttpToolDef(tenantId, "Get Health", "GET");
        var sut = CreateSut(tenantId, included, excluded);

        var tools = await sut.BuildToolsAsync(enabledToolIds: [included.Id]);

        Assert.Equal(["Get_Status"], tools.Select(t => t.Name));
    }

    [Fact]
    public async Task BuildToolsAsync_NameCollisionAfterSanitizing_KeepsFirstOnly()
    {
        var tenantId = Guid.NewGuid();
        var first = CreateHttpToolDef(tenantId, "Get-Status", "GET");
        var second = CreateHttpToolDef(tenantId, "Get_Status", "GET"); // sanitizes to the same name
        var sut = CreateSut(tenantId, first, second);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Single(tools);
    }

    [Fact]
    public async Task BuildToolsAsync_NoAmbientTenant_ReturnsEmpty()
    {
        var sut = new MafToolFunctionFactory(new FakeToolDefinitionRepository(), null!, null!,
            new FakeCurrentUserService { TenantId = null }, NullLogger<MafToolFunctionFactory>.Instance);

        var tools = await sut.BuildToolsAsync(enabledToolIds: null);

        Assert.Empty(tools);
    }
}
