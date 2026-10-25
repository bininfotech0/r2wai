namespace R2WAI.Application.Features.Chat.Queries;

public record GetConversationByIdQuery : IRequest<ConversationDto>
{
    public Guid Id { get; init; }
}

public class GetConversationByIdQueryHandler(
    IRepository<Conversation> conversationRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetConversationByIdQuery, ConversationDto>
{
    public async Task<ConversationDto> Handle(GetConversationByIdQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var conversation = await conversationRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), query.Id);

        if (conversation.TenantId != tenantId || conversation.UserId != userId)
            throw new NotFoundException(nameof(Conversation), query.Id);

        if (conversation.IsDeleted)
            throw new NotFoundException(nameof(Conversation), query.Id);

        return mapper.Map<ConversationDto>(conversation);
    }
}
