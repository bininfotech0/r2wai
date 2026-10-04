namespace R2WAI.Application.Features.BusinessCapabilities.Commands;

public record DeleteBusinessCapabilityCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteBusinessCapabilityCommandHandler(
    IRepository<BusinessCapability> capabilityRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteBusinessCapabilityCommand, Unit>
{
    public async Task<Unit> Handle(DeleteBusinessCapabilityCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCapability), command.Id);

        capabilityRepo.Delete(capability);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
