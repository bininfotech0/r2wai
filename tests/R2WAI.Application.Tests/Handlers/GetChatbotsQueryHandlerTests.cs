using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class GetChatbotsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPagedResults()
    {
        var tenantId = Guid.NewGuid();
        var chatbots = new List<Chatbot>
        {
            new(Guid.NewGuid(), tenantId, Guid.NewGuid(), "Bot 1", null, null),
            new(Guid.NewGuid(), tenantId, Guid.NewGuid(), "Bot 2", null, null),
        };

        var repoMock = new Mock<IRepository<Chatbot>>();
        repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Chatbot, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatbots);

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(x => x.TenantId).Returns(tenantId);

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(m => m.Map<ChatbotDto>(It.IsAny<Chatbot>()))
            .Returns((Chatbot c) => new ChatbotDto { Id = c.Id, Name = c.Name });

        var handler = new GetChatbotsQueryHandler(
            repoMock.Object, currentUserMock.Object, mapperMock.Object);

        var query = new GetChatbotsQuery { Page = 1, PageSize = 20 };
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
    }
}
