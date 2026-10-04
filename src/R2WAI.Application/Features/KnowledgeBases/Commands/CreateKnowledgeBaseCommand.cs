using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record CreateKnowledgeBaseCommand : IRequest<KnowledgeBaseDto>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string DataClassification { get; init; } = "Internal";
}

public class CreateKnowledgeBaseCommandValidator : AbstractValidator<CreateKnowledgeBaseCommand>
{
    public CreateKnowledgeBaseCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
        RuleFor(v => v.DataClassification).Must(v => Enum.TryParse<Domain.Enums.DataClassification>(v, true, out _))
            .WithMessage("DataClassification must be one of: Public, Internal, Confidential, Restricted.");
    }
}

public class CreateKnowledgeBaseCommandHandler(
    IKnowledgeBaseService knowledgeBaseService,
    ICurrentUserService currentUser) : IRequestHandler<CreateKnowledgeBaseCommand, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(CreateKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        return await knowledgeBaseService.CreateKnowledgeBaseAsync(
            tenantId, userId, command.Name, command.Description, command.DataClassification, cancellationToken);
    }
}
