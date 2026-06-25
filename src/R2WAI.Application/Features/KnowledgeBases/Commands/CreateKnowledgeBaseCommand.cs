using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record CreateKnowledgeBaseCommand : IRequest<KnowledgeBaseDto>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public class CreateKnowledgeBaseCommandValidator : AbstractValidator<CreateKnowledgeBaseCommand>
{
    public CreateKnowledgeBaseCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}

public class CreateKnowledgeBaseCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateKnowledgeBaseCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(CreateKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var kb = new KnowledgeBase(Guid.NewGuid(), tenantId, userId, command.Name, command.Description);
        kb.UpdateStatus(KnowledgeBaseStatus.Active);

        await kbRepo.AddAsync(kb, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseDto>(kb);
    }
}
