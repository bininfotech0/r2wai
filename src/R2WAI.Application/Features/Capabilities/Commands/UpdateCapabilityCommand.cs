using FluentValidation;

namespace R2WAI.Application.Features.Capabilities.Commands;

public record UpdateCapabilityCommand : IRequest<CapabilityDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ApplicationApiId { get; init; }
    public string? HttpMethod { get; init; }
    public string? EndpointPath { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public string? RequiredRole { get; init; }
    public bool ConfirmationRequired { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool AuditRequired { get; init; } = true;
}

public class UpdateCapabilityCommandValidator : AbstractValidator<UpdateCapabilityCommand>
{
    public UpdateCapabilityCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.RiskLevel).NotEmpty().Must(r => new[] { "Low", "Medium", "High", "Critical" }.Contains(r))
            .WithMessage("RiskLevel must be one of: Low, Medium, High, Critical");
        RuleFor(v => v.HttpMethod)
            .Must(m => new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }.Contains(m))
            .WithMessage("HttpMethod must be one of: GET, POST, PUT, PATCH, DELETE")
            .When(v => v.HttpMethod is not null);
        RuleFor(v => v.EndpointPath).MaximumLength(500)
            .Must(p => p!.StartsWith('/')).WithMessage("EndpointPath must start with '/'.")
            .When(v => !string.IsNullOrEmpty(v.EndpointPath));
    }
}

public class UpdateCapabilityCommandHandler(
    IRepository<ToolDefinition> capabilityRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateCapabilityCommand, CapabilityDto>
{
    public async Task<CapabilityDto> Handle(UpdateCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        capability.Update(command.Name, command.Description, ToolType.Http, capability.EndpointUrl, capability.Configuration);
        capability.LinkApi(command.ApplicationApiId, command.HttpMethod, command.EndpointPath);
        capability.ConfigureGovernance(command.RiskLevel, command.RequiredRole,
            command.ConfirmationRequired, command.ApprovalRequired, command.AuditRequired);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<CapabilityDto>(capability);
    }
}
