using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class CreateConversationCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesConversation()
    {
        var repoMock = new Mock<IRepository<Conversation>>();
        var uowMock = new Mock<IUnitOfWork>();
        var mapperMock = new Mock<IMapper>();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        currentUserMock.Setup(x => x.TenantId).Returns(Guid.NewGuid());

        var handler = new CreateConversationCommandHandler(
            repoMock.Object, uowMock.Object, currentUserMock.Object, mapperMock.Object);

        var command = new CreateConversationCommand
        {
            Title = "Test Conversation",
            Module = "chat",
        };

        var result = await handler.Handle(command, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
