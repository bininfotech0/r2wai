using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record UpdateKnowledgeBaseCommand : IRequest<KnowledgeBaseDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public class UpdateKnowledgeBaseCommandValidator : AbstractValidator<UpdateKnowledgeBaseCommand>
{
    public UpdateKnowledgeBaseCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateKnowledgeBaseCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateKnowledgeBaseCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(UpdateKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        var kb = await kbRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), command.Id);

        kb.UpdateStatus(KnowledgeBaseStatus.Active);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseDto>(kb);
    }
}
