namespace R2WAI.Application.Features.Capabilities.Commands;

public record DeleteCapabilityCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteCapabilityCommandHandler(
    IRepository<ToolDefinition> capabilityRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCapabilityCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        capabilityRepo.Delete(capability);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
