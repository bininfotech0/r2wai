using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace R2WAI.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Processing request {RequestName}: {@Request}", requestName, request);

        var response = await next();

        stopwatch.Stop();
        logger.LogInformation("Completed request {RequestName} in {ElapsedMs}ms: {@Response}",
            requestName, stopwatch.ElapsedMilliseconds, response);

        return response;
    }
}
