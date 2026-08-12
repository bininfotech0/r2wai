namespace R2WAI.Application.Features.Capabilities.Commands;

public record SetCapabilityActiveCommand : IRequest<CapabilityDto>
{
    public Guid Id { get; init; }
    public bool IsActive { get; init; }
}

public class SetCapabilityActiveCommandHandler(
    IRepository<ToolDefinition> capabilityRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<SetCapabilityActiveCommand, CapabilityDto>
{
    public async Task<CapabilityDto> Handle(SetCapabilityActiveCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        if (command.IsActive) capability.Activate();
        else capability.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<CapabilityDto>(capability);
    }
}
