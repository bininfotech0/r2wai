namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record AssignKnowledgeBaseApplicationCommand : IRequest<KnowledgeBaseDto>
{
    public Guid Id { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class AssignKnowledgeBaseApplicationCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<AssignKnowledgeBaseApplicationCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(AssignKnowledgeBaseApplicationCommand command, CancellationToken cancellationToken)
    {
        var kb = await kbRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), command.Id);

        kb.AssignApplication(command.ApplicationId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseDto>(kb);
    }
}
