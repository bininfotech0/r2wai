using System.Linq.Expressions;
using AutoMapper;
using Moq;
using R2WAI.Application.Common.AI;
using R2WAI.Application.Features.Capabilities.Commands;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// Capability names must be unique tenant-wide by their *sanitized* function name — the runtime
/// silently drops the second of two capabilities that sanitize to the same name. The repository
/// mock evaluates the handler's real predicate against an in-memory set.
/// </summary>
public class CapabilityNameUniquenessTests
{
    private readonly Mock<IRepository<ToolDefinition>> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly List<ToolDefinition> _existing = [];

    public CapabilityNameUniquenessTests()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _repoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<ToolDefinition, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns<Expression<Func<ToolDefinition, bool>>, CancellationToken>(
                (predicate, _) => Task.FromResult((IReadOnlyList<ToolDefinition>)_existing.AsQueryable().Where(predicate).ToList()));
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) => Task.FromResult(_existing.FirstOrDefault(t => t.Id == id)));
    }

    private ToolDefinition Seed(string name)
    {
        var tool = new ToolDefinition(Guid.NewGuid(), _tenantId, name, ToolType.Http, null);
        _existing.Add(tool);
        return tool;
    }

    private CreateCapabilityCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object, _currentUserMock.Object, new Mock<IMapper>().Object);

    [Theory]
    [InlineData("getUser")]
    [InlineData("GETUSER")]
    [InlineData(" getUser ")]
    public async Task Create_SameNameIgnoringCaseAndWhitespace_Throws(string name)
    {
        Seed("getUser");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().Handle(new CreateCapabilityCommand { Name = name }, CancellationToken.None));

        Assert.Contains(ex.Errors, kv => kv.Key == "Name");
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_NameCollidingOnlyAfterSanitizing_ThrowsWithExplanation()
    {
        Seed("get user");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().Handle(new CreateCapabilityCommand { Name = "get-user" }, CancellationToken.None));

        Assert.Contains("get_user", ex.Errors["Name"][0]);
    }

    [Fact]
    public async Task Create_OtherTenantsCapability_DoesNotClash()
    {
        _existing.Add(new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "getUser", ToolType.Http, null));

        await CreateHandler().Handle(new CreateCapabilityCommand { Name = "getUser" }, CancellationToken.None);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_KeepingOwnName_Succeeds_ButTakingAnothersFails()
    {
        var mine = Seed("getUser");
        Seed("listUsers");
        var handler = new UpdateCapabilityCommandHandler(_repoMock.Object, _uowMock.Object, new Mock<IMapper>().Object);

        await handler.Handle(new UpdateCapabilityCommand { Id = mine.Id, Name = "GetUser" }, CancellationToken.None);
        Assert.Equal("GetUser", mine.Name);

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UpdateCapabilityCommand { Id = mine.Id, Name = "listUsers" }, CancellationToken.None));
    }

    [Fact]
    public void MakeUnique_WithoutPrefix_FallsBackToNumericSuffix()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "getUser" };

        Assert.Equal("getUser_2", CapabilityNames.MakeUnique("getUser", prefix: null, taken));
        Assert.Equal("getUser_3", CapabilityNames.MakeUnique("getUser", prefix: null, taken));
    }

    [Fact]
    public void MakeUnique_PrefixWithSpaces_StillYieldsDistinctFunctionName()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "create_issue" };

        var name = CapabilityNames.MakeUnique("create_issue", "GitHub MCP", taken);

        Assert.Equal("GitHub MCP_create_issue", name);
        Assert.Contains("GitHub_MCP_create_issue", taken);
    }
}
