using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.DTOs;
using R2WAI.Application.Features.Assistants.Mappings;
using R2WAI.Application.Features.Assistants.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// docs/api/MISSING-BACKEND-ENDPOINTS.md §4 item 7 — GET /assistants gained real server-side
/// publishStatus/sortBy and tenant-wide status counts. These tests prove the handler's own new
/// logic (filter, sort switch, counts scoped-but-not-narrowed) rather than just that it compiles.
/// </summary>
public class GetAssistantsQueryHandlerTests
{
    private readonly Mock<IRepository<AssistantDefinition>> _repoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly IMapper _mapper;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetAssistantsQueryHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<AssistantProfile>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
        _currentUserMock.Setup(c => c.TenantId).Returns(_tenantId);
        _cacheMock.Setup(c => c.GetAsync<AssistantsPagedResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantsPagedResult?)null);
    }

    private GetAssistantsQueryHandler CreateHandler() =>
        new(_repoMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapper);

    private AssistantDefinition Assistant(string name, PublishStatus status, int usageCount, DateTime createdAt)
    {
        var a = new AssistantDefinition(Guid.NewGuid(), _tenantId, name, AssistantType.General);
        if (status == PublishStatus.Published) a.Publish();
        else if (status == PublishStatus.Archived) a.Archive();
        for (var i = 0; i < usageCount; i++) a.IncrementUsageCount();
        // CreatedAt's setter is `protected` (BaseEntity<TId>) — the constructor always stamps
        // DateTime.UtcNow with no public override, so distinguishing fixture timestamps for the
        // "recent" sort test needs reflection, not a real API misuse.
        typeof(AssistantDefinition).GetProperty(nameof(AssistantDefinition.CreatedAt))!
            .SetValue(a, createdAt, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
        return a;
    }

    private void SeedRepo(params AssistantDefinition[] assistants) =>
        _repoMock.Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistants);

    [Fact]
    public async Task Handle_PublishStatusFilter_ReturnsOnlyMatchingItems()
    {
        SeedRepo(
            Assistant("Zeta", PublishStatus.Draft, 5, DateTime.UtcNow),
            Assistant("Alpha", PublishStatus.Published, 50, DateTime.UtcNow),
            Assistant("Mid", PublishStatus.Published, 20, DateTime.UtcNow),
            Assistant("Beta", PublishStatus.Archived, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAssistantsQuery { PublishStatus = PublishStatus.Published }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal("Published", i.PublishStatus));
    }

    [Fact]
    public async Task Handle_StatusCounts_ReflectsFullScopeNotJustTheFilteredResult()
    {
        SeedRepo(
            Assistant("Zeta", PublishStatus.Draft, 5, DateTime.UtcNow),
            Assistant("Alpha", PublishStatus.Published, 50, DateTime.UtcNow),
            Assistant("Mid", PublishStatus.Published, 20, DateTime.UtcNow),
            Assistant("Beta", PublishStatus.Archived, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        // Filtering to Published alone must not shrink the OTHER statuses' counts to zero.
        var result = await handler.Handle(new GetAssistantsQuery { PublishStatus = PublishStatus.Published }, CancellationToken.None);

        Assert.Equal(1, result.StatusCounts["Draft"]);
        Assert.Equal(2, result.StatusCounts["Published"]);
        Assert.Equal(1, result.StatusCounts["Archived"]);
    }

    [Theory]
    [InlineData("name", new[] { "Alpha", "Beta", "Mid", "Zeta" })]
    [InlineData("usage", new[] { "Alpha", "Mid", "Zeta", "Beta" })]
    [InlineData("least-used", new[] { "Beta", "Zeta", "Mid", "Alpha" })]
    public async Task Handle_SortBy_OrdersCorrectly(string sortBy, string[] expected)
    {
        SeedRepo(
            Assistant("Zeta", PublishStatus.Draft, 5, DateTime.UtcNow),
            Assistant("Alpha", PublishStatus.Published, 50, DateTime.UtcNow),
            Assistant("Mid", PublishStatus.Published, 20, DateTime.UtcNow),
            Assistant("Beta", PublishStatus.Archived, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAssistantsQuery { SortBy = sortBy, PageSize = 100 }, CancellationToken.None);

        Assert.Equal(expected, result.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Handle_DefaultSort_OrdersByCreatedAtDescending()
    {
        var now = DateTime.UtcNow;
        SeedRepo(
            Assistant("Old", PublishStatus.Draft, 0, now.AddDays(-2)),
            Assistant("New", PublishStatus.Draft, 0, now),
            Assistant("Mid", PublishStatus.Draft, 0, now.AddDays(-1)));
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAssistantsQuery { PageSize = 100 }, CancellationToken.None);

        Assert.Equal(["New", "Mid", "Old"], result.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Handle_NoFilters_UsesCache()
    {
        var cached = new AssistantsPagedResult { Items = [], TotalCount = 0, Page = 1, PageSize = 20 };
        _cacheMock.Setup(c => c.GetAsync<AssistantsPagedResult>($"assistants:{_tenantId}:p1:s20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAssistantsQuery(), CancellationToken.None);

        Assert.Same(cached, result);
        _repoMock.Verify(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<AssistantDefinition, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPublishStatusFilter_SkipsCache()
    {
        SeedRepo(Assistant("A", PublishStatus.Draft, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        await handler.Handle(new GetAssistantsQuery { PublishStatus = PublishStatus.Draft }, CancellationToken.None);

        _cacheMock.Verify(c => c.GetAsync<AssistantsPagedResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<AssistantsPagedResult>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithSortByOverride_SkipsCache()
    {
        SeedRepo(Assistant("A", PublishStatus.Draft, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        await handler.Handle(new GetAssistantsQuery { SortBy = "name" }, CancellationToken.None);

        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<AssistantsPagedResult>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExplicitRecentSort_IsStillCacheable()
    {
        // "recent" is the default — a client that sends it explicitly must hit the same cache
        // path as a client that omits sortBy entirely, or the cache key space quietly doubles.
        SeedRepo(Assistant("A", PublishStatus.Draft, 0, DateTime.UtcNow));
        var handler = CreateHandler();

        await handler.Handle(new GetAssistantsQuery { SortBy = "recent" }, CancellationToken.None);

        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<AssistantsPagedResult>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
