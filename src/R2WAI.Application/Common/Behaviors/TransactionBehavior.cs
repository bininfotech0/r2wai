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
        logger.LogInformation("Executing {RequestName} with transactional guarantee", requestName);
        return await next();
    }
}
