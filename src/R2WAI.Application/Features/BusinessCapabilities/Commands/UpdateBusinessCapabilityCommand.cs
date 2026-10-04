using FluentValidation;
using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Commands;

public record UpdateBusinessCapabilityCommand : IRequest<BusinessCapabilityDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public List<Guid>? ToolIds { get; init; }
    public List<Guid>? KnowledgeBaseIds { get; init; }
    public List<Guid>? WorkflowIds { get; init; }
    public List<Guid>? ApplicationApiIds { get; init; }
}

public class UpdateBusinessCapabilityCommandValidator : AbstractValidator<UpdateBusinessCapabilityCommand>
{
    public UpdateBusinessCapabilityCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class UpdateBusinessCapabilityCommandHandler(
    IRepository<BusinessCapability> capabilityRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateBusinessCapabilityCommand, BusinessCapabilityDto>
{
    public async Task<BusinessCapabilityDto> Handle(UpdateBusinessCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCapability), command.Id);

        capability.UpdateDetails(command.Name, command.Description, command.Icon);
        capability.LinkResources(command.ToolIds, command.KnowledgeBaseIds, command.WorkflowIds, command.ApplicationApiIds);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<BusinessCapabilityDto>(capability);
    }
}
