using AutoMapper;
using Moq;
using R2WAI.Application.Features.Assistants.DTOs;
using R2WAI.Application.Features.Assistants.Queries;

namespace R2WAI.Application.Tests.Handlers;

public class GetAssistantVersionsQueryHandlerTests
{
    private readonly Mock<IRepository<AssistantVersion>> _versionRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    [Fact]
    public async Task Handle_ReturnsVersionsOrderedNewestFirst()
    {
        var assistantId = Guid.NewGuid();
        var v1 = AssistantVersion.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), assistantId, 1, "{}");
        var v2 = AssistantVersion.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), assistantId, 2, "{}");

        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AssistantVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AssistantVersion>)[v1, v2]);
        _mapperMock.Setup(m => m.Map<List<AssistantVersionDto>>(It.IsAny<List<AssistantVersion>>()))
            .Returns((List<AssistantVersion> src) => src.Select(v => new AssistantVersionDto { Id = v.Id, VersionNumber = v.VersionNumber }).ToList());

        var handler = new GetAssistantVersionsQueryHandler(_versionRepoMock.Object, _mapperMock.Object);
        var result = await handler.Handle(new GetAssistantVersionsQuery { AssistantDefinitionId = assistantId }, CancellationToken.None);

        Assert.Equal([2, 1], result.Select(v => v.VersionNumber));
    }
}
