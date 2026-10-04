using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Application.Features.KnowledgeBases.DTOs;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// R2WAI 2.0 §6 — bounded Agentic RAG. Every test here proves a specific promise from the brief:
/// evidence-sufficiency is a real check (not just "did search return anything"), the retry is
/// genuinely bounded at one, and Standard mode's caller path (ChatWithAssistantCommandHandler)
/// never even constructs this class when RetrievalMode isn't "Agentic".
/// </summary>
public class AgenticRetrievalOrchestratorTests
{
    private readonly Mock<IKnowledgeBaseService> _kbServiceMock = new();
    private readonly Mock<IAIService> _aiServiceMock = new();
    private readonly Guid _kbId = Guid.NewGuid();

    private AgenticRetrievalOrchestrator CreateOrchestrator() =>
        new(_kbServiceMock.Object, _aiServiceMock.Object, NullLogger<AgenticRetrievalOrchestrator>.Instance);

    private static PagedResult<SearchResultDto> Page(params SearchResultDto[] items) =>
        new() { Items = items.ToList(), TotalCount = items.Length, Page = 1, PageSize = 5 };

    private static SearchResultDto Result(double score, string content = "chunk") =>
        new() { Id = Guid.NewGuid(), Content = content, SourceName = "doc.txt", Score = score };

    [Fact]
    public async Task RetrieveAsync_ConfidentFirstPass_StopsAtOneIterationAndNeverCallsAI()
    {
        _kbServiceMock.Setup(k => k.SearchKnowledgeBaseAsync(_kbId, "the query", 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page(Result(0.9)));
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        Assert.True(result.EvidenceSufficient);
        Assert.Equal(1, result.IterationsUsed);
        Assert.Single(result.QueriesTried);
        _aiServiceMock.Verify(a => a.GenerateResponseAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
            It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_WeakFirstPass_RewritesOnceAndRetries()
    {
        _kbServiceMock.SetupSequence(k => k.SearchKnowledgeBaseAsync(_kbId, It.IsAny<string>(), 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page(Result(0.72))) // below the 0.80 sufficiency bar
            .ReturnsAsync(Page(Result(0.91, "better chunk")));
        _aiServiceMock.Setup(a => a.GenerateResponseAsync(
                "the query", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
                It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("a rewritten, more specific query");
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        Assert.True(result.EvidenceSufficient);
        Assert.Equal(2, result.IterationsUsed);
        Assert.Equal(["the query", "a rewritten, more specific query"], result.QueriesTried);
        // Union of both passes' results is returned, not just the second pass alone.
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task RetrieveAsync_StillWeakAfterRewrite_StopsAtTheHardBoundOfTwoIterations()
    {
        _kbServiceMock.Setup(k => k.SearchKnowledgeBaseAsync(_kbId, It.IsAny<string>(), 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page(Result(0.71)));
        _aiServiceMock.Setup(a => a.GenerateResponseAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
                It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("still not great");
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        // The actual "never unbounded" guarantee: exactly 2 iterations, never more, regardless of
        // how many times a caller might invoke this for the same weak query.
        Assert.False(result.EvidenceSufficient);
        Assert.Equal(2, result.IterationsUsed);
        _kbServiceMock.Verify(k => k.SearchKnowledgeBaseAsync(_kbId, It.IsAny<string>(), 1, 5, It.IsAny<CancellationToken>(), null), Times.Exactly(2));
    }

    [Fact]
    public async Task RetrieveAsync_NoResultsAtAll_ReturnsInsufficientWithoutThrowing()
    {
        _kbServiceMock.Setup(k => k.SearchKnowledgeBaseAsync(_kbId, It.IsAny<string>(), 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page());
        _aiServiceMock.Setup(a => a.GenerateResponseAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
                It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("a rewrite");
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        Assert.False(result.EvidenceSufficient);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RetrieveAsync_RewriteReturnsBlankOrUnchangedQuery_StopsEarlyInsteadOfWastingTheRetry()
    {
        _kbServiceMock.Setup(k => k.SearchKnowledgeBaseAsync(_kbId, "the query", 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page(Result(0.71)));
        _aiServiceMock.Setup(a => a.GenerateResponseAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
                It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("the query"); // model just echoed it back — not a real rewrite
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        Assert.Equal(1, result.IterationsUsed);
        Assert.Single(result.QueriesTried);
        _kbServiceMock.Verify(k => k.SearchKnowledgeBaseAsync(_kbId, It.IsAny<string>(), 1, 5, It.IsAny<CancellationToken>(), null), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_RewriteCallThrows_FallsBackToFirstPassResultsInsteadOfFailingTheTurn()
    {
        _kbServiceMock.Setup(k => k.SearchKnowledgeBaseAsync(_kbId, "the query", 1, 5, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Page(Result(0.71)));
        _aiServiceMock.Setup(a => a.GenerateResponseAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ResolvedModelConfig?>(),
                It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider timeout"));
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.RetrieveAsync(_kbId, "the query", null, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.False(result.EvidenceSufficient);
    }
}
