using AutoMapper;
using Moq;
using R2WAI.Application.Features.Chatbots.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class UpdateChatbotWidgetCommandHandlerTests
{
    private readonly Mock<IRepository<Chatbot>> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    public UpdateChatbotWidgetCommandHandlerTests()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ExistingChatbot_PersistsEmbedScriptAndWidgetSettings()
    {
        var chatbot = new Chatbot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Bot");
        _repoMock.Setup(r => r.GetByIdAsync(chatbot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chatbot);
        _mapperMock.Setup(m => m.Map<ChatbotDto>(chatbot)).Returns(new ChatbotDto { Id = chatbot.Id });

        var handler = new UpdateChatbotWidgetCommandHandler(
            _repoMock.Object, _uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapperMock.Object);

        var command = new UpdateChatbotWidgetCommand
        {
            Id = chatbot.Id,
            EmbedScript = "<script data-color=\"#2563eb\"></script>",
            WidgetSettings = "{\"title\":\"Support\",\"color\":\"#2563eb\",\"position\":\"bottom-right\"}",
        };

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("<script data-color=\"#2563eb\"></script>", chatbot.EmbedScript);
        Assert.Equal("{\"title\":\"Support\",\"color\":\"#2563eb\",\"position\":\"bottom-right\"}", chatbot.WidgetSettings);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownChatbot_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Chatbot?)null);

        var handler = new UpdateChatbotWidgetCommandHandler(
            _repoMock.Object, _uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapperMock.Object);

        var command = new UpdateChatbotWidgetCommand { Id = Guid.NewGuid(), EmbedScript = "<script></script>", WidgetSettings = "{}" };

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
