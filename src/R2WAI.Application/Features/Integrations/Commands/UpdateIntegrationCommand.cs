using FluentValidation;

namespace R2WAI.Application.Features.Integrations.Commands;

public record UpdateIntegrationCommand : IRequest<Guid>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? EndpointUrl { get; init; }
    public string? Configuration { get; init; }
}

public class UpdateIntegrationCommandValidator : AbstractValidator<UpdateIntegrationCommand>
{
    public UpdateIntegrationCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Type).NotEmpty();
    }
}
