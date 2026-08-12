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
    IKnowledgeBaseService knowledgeBaseService) : IRequestHandler<AddSourceCommand, KnowledgeBaseSourceDto>
{
    public Task<KnowledgeBaseSourceDto> Handle(AddSourceCommand command, CancellationToken cancellationToken) =>
        knowledgeBaseService.AddSourceAsync(
            command.KnowledgeBaseId, command.Type, command.ReferenceId, command.Url, command.Content, cancellationToken);
}
