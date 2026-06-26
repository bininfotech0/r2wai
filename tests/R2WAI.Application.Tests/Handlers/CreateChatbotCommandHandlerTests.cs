using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class CreateChatbotCommandHandlerTests
{
    private readonly Mock<IRepository<Chatbot>> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    public CreateChatbotCommandHandlerTests()
    {
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(x => x.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesChatbot()
    {
        var handler = new CreateChatbotCommandHandler(
            _repoMock.Object, _uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapperMock.Object);

        var command = new CreateChatbotCommand
        {
            Name = "Test Bot",
            KnowledgeBaseId = Guid.NewGuid(),
        };

        var result = await handler.Handle(command, CancellationToken.None);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Chatbot>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingUserId_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        var handler = new CreateChatbotCommandHandler(
            _repoMock.Object, _uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapperMock.Object);

        var command = new CreateChatbotCommand { Name = "Test" };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
