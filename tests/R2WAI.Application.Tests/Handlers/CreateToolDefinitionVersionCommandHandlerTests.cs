using AutoMapper;
using Moq;
using R2WAI.Application.Features.Capabilities.Commands;
using R2WAI.Application.Features.Capabilities.DTOs;

namespace R2WAI.Application.Tests.Handlers;

public class CreateToolDefinitionVersionCommandHandlerTests
{
    private readonly Mock<IRepository<ToolDefinitionVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<ToolDefinition>> _toolRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ToolDefinition _tool;

    public CreateToolDefinitionVersionCommandHandlerTests()
    {
        _tool = new ToolDefinition(Guid.NewGuid(), _tenantId, "Send Email", ToolType.Email, "desc");

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _toolRepoMock.Setup(r => r.GetByIdAsync(_tool.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tool);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ToolDefinitionVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ToolDefinitionVersion>)[]);
    }

    private CreateToolDefinitionVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _toolRepoMock.Object, _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesFirstVersion()
    {
        var handler = CreateHandler();
        ToolDefinitionVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<ToolDefinitionVersion>(), It.IsAny<CancellationToken>()))
            .Callback<ToolDefinitionVersion, CancellationToken>((v, _) => captured = v)
            .Returns<ToolDefinitionVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new CreateToolDefinitionVersionCommand { ToolDefinitionId = _tool.Id, Publish = true };

        await handler.Handle(command, CancellationToken.None);

        _versionRepoMock.Verify(r => r.AddAsync(It.IsAny<ToolDefinitionVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Contains("Send Email", captured.ConfigSnapshot);
    }

    [Fact]
    public async Task Handle_MissingTenant_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns((Guid?)null);
        var handler = CreateHandler();

        var command = new CreateToolDefinitionVersionCommand { ToolDefinitionId = _tool.Id };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ToolNotFound_ThrowsNotFound()
    {
        _toolRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolDefinition?)null);
        var handler = CreateHandler();

        var command = new CreateToolDefinitionVersionCommand { ToolDefinitionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
