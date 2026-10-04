using R2WAI.Application.Features.Integrations.Queries;

namespace R2WAI.Application.Tests.Handlers;

public class GetIntegrationCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNonEmptyCatalogWithUniqueIds()
    {
        var handler = new GetIntegrationCatalogQueryHandler();

        var result = await handler.Handle(new GetIntegrationCatalogQuery(), CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(result.Count, result.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public async Task Handle_EveryEntrySuggestsHttp_TheOnlyTypeThatIsActuallyCallable()
    {
        var handler = new GetIntegrationCatalogQueryHandler();

        var result = await handler.Handle(new GetIntegrationCatalogQuery(), CancellationToken.None);

        Assert.All(result, e => Assert.Equal("Http", e.SuggestedType));
    }
}
