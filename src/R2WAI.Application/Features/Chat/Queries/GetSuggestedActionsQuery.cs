namespace R2WAI.Application.Features.Chat.Queries;

public record GetSuggestedActionsQuery : IRequest<IReadOnlyList<SuggestedActionDto>>
{
    public Guid? ConversationId { get; init; }
}

public class GetSuggestedActionsQueryHandler : IRequestHandler<GetSuggestedActionsQuery, IReadOnlyList<SuggestedActionDto>>
{
    private static readonly List<SuggestedActionDto> DefaultActions =
    [
        new() { Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"), Title = "Summarize document", Description = "Summarize the latest uploaded document", Icon = "document-text" },
        new() { Id = Guid.Parse("a4444444-4444-4444-4444-444444444444"), Title = "Analyze requirements", Description = "Extract requirements from a document", Icon = "search" },
        new() { Id = Guid.Parse("a6666666-6666-6666-6666-666666666666"), Title = "Generate report", Description = "Generate a business report or summary", Icon = "file-text" },
        new() { Id = Guid.Parse("a7777777-7777-7777-7777-777777777777"), Title = "Approve pending items", Description = "View and act on pending approvals", Icon = "check-circle" },
    ];

    public Task<IReadOnlyList<SuggestedActionDto>> Handle(GetSuggestedActionsQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<SuggestedActionDto>>(DefaultActions);
    }
}
