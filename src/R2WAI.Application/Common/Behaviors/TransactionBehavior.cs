using MediatR;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
            return await next();

        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Beginning transaction for {RequestName}", requestName);

        TResponse response = default!;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                response = await next();
            }, cancellationToken);
            logger.LogInformation("Committed transaction for {RequestName}", requestName);
            return response;
        }
        catch
        {
            logger.LogWarning("Rolled back transaction for {RequestName}", requestName);
            throw;
        }
    }
}
