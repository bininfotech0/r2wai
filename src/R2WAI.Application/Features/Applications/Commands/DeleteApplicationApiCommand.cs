namespace R2WAI.Application.Features.Applications.Commands;

public record DeleteApplicationApiCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteApplicationApiCommandHandler(
    IRepository<ApplicationApi> apiRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteApplicationApiCommand, Unit>
{
    public async Task<Unit> Handle(DeleteApplicationApiCommand command, CancellationToken cancellationToken)
    {
        var api = await apiRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationApi), command.Id);

        apiRepo.Delete(api);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
