using FluentValidation;

namespace R2WAI.Application.Features.Integrations.Commands;

public record CreateIntegrationCommand : IRequest<Guid>
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? EndpointUrl { get; init; }
    public string? Configuration { get; init; }
}

public class CreateIntegrationCommandValidator : AbstractValidator<CreateIntegrationCommand>
{
    public CreateIntegrationCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Type).NotEmpty();
    }
}
