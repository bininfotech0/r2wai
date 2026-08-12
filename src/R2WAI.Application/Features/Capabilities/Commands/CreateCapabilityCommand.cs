using FluentValidation;

namespace R2WAI.Application.Features.Capabilities.Commands;

public record CreateCapabilityCommand : IRequest<CapabilityDto>
{
    public Guid? ApplicationId { get; init; }
    public Guid? ApplicationApiId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? HttpMethod { get; init; }
    public string? EndpointPath { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public string? RequiredRole { get; init; }
    public bool ConfirmationRequired { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool AuditRequired { get; init; } = true;
}

public class CreateCapabilityCommandValidator : AbstractValidator<CreateCapabilityCommand>
{
    public CreateCapabilityCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.RiskLevel).NotEmpty().Must(r => new[] { "Low", "Medium", "High", "Critical" }.Contains(r))
            .WithMessage("RiskLevel must be one of: Low, Medium, High, Critical");
    }
}

public class CreateCapabilityCommandHandler(
    IRepository<ToolDefinition> capabilityRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateCapabilityCommand, CapabilityDto>
{
    public async Task<CapabilityDto> Handle(CreateCapabilityCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var capability = new ToolDefinition(Guid.NewGuid(), tenantId, command.Name, ToolType.Http, command.Description);
        capability.AssignApplication(command.ApplicationId);
        capability.LinkApi(command.ApplicationApiId, command.HttpMethod, command.EndpointPath);
        capability.ConfigureGovernance(command.RiskLevel, command.RequiredRole,
            command.ConfirmationRequired, command.ApprovalRequired, command.AuditRequired);

        await capabilityRepo.AddAsync(capability, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<CapabilityDto>(capability);
    }
}
