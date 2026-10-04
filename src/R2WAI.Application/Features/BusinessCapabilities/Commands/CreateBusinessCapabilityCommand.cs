using FluentValidation;
using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Commands;

public record CreateBusinessCapabilityCommand : IRequest<BusinessCapabilityDto>
{
    public Guid AssistantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public List<Guid>? ToolIds { get; init; }
    public List<Guid>? KnowledgeBaseIds { get; init; }
    public List<Guid>? WorkflowIds { get; init; }
    public List<Guid>? ApplicationApiIds { get; init; }
}

public class CreateBusinessCapabilityCommandValidator : AbstractValidator<CreateBusinessCapabilityCommand>
{
    public CreateBusinessCapabilityCommandValidator()
    {
        RuleFor(v => v.AssistantId).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class CreateBusinessCapabilityCommandHandler(
    IRepository<BusinessCapability> capabilityRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateBusinessCapabilityCommand, BusinessCapabilityDto>
{
    public async Task<BusinessCapabilityDto> Handle(CreateBusinessCapabilityCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var assistant = await assistantRepo.GetByIdAsync(command.AssistantId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);
        if (assistant.TenantId != tenantId) throw new UnauthorizedException();

        var capability = new BusinessCapability(Guid.NewGuid(), tenantId, command.AssistantId, command.Name, command.Description);
        capability.UpdateDetails(command.Name, command.Description, command.Icon);
        capability.LinkResources(command.ToolIds, command.KnowledgeBaseIds, command.WorkflowIds, command.ApplicationApiIds);

        await capabilityRepo.AddAsync(capability, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<BusinessCapabilityDto>(capability);
    }
}
