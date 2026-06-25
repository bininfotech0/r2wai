using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record AddSourceCommand : IRequest<KnowledgeBaseSourceDto>
{
    public Guid KnowledgeBaseId { get; init; }
    public string Type { get; init; } = string.Empty;
    public Guid? ReferenceId { get; init; }
    public string? Url { get; init; }
    public string? Content { get; init; }
}

public class AddSourceCommandValidator : AbstractValidator<AddSourceCommand>
{
    public AddSourceCommandValidator()
    {
        RuleFor(v => v.KnowledgeBaseId).NotEmpty();
        RuleFor(v => v.Type).NotEmpty().MaximumLength(50);
    }
}

public class AddSourceCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IRepository<KnowledgeBaseSource> sourceRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<AddSourceCommand, KnowledgeBaseSourceDto>
{
    public async Task<KnowledgeBaseSourceDto> Handle(AddSourceCommand command, CancellationToken cancellationToken)
    {
        var kb = await kbRepo.GetByIdAsync(command.KnowledgeBaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), command.KnowledgeBaseId);

        var source = new KnowledgeBaseSource(
            Guid.NewGuid(), command.KnowledgeBaseId, command.Type,
            command.ReferenceId, command.Url, command.Content);

        kb.AddSource(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseSourceDto>(source);
    }
}
