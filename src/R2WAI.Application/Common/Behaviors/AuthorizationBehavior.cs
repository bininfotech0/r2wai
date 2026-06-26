using MediatR;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUserService,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuthorizedRequest authorizedRequest)
            return await next();

        if (!currentUserService.IsAuthenticated)
            throw new UnauthorizedException("Authentication is required.");

        if (currentUserService.TenantId is null)
            throw new UnauthorizedException("Tenant context is required.");

        var requiredRoles = authorizedRequest.RequiredRoles;
        if (requiredRoles.Length > 0)
        {
            var userRoles = currentUserService.Roles;
            var hasRole = requiredRoles.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
            if (!hasRole)
            {
                logger.LogWarning(
                    "Authorization failed for {RequestName}: user lacks required roles [{Roles}]",
                    typeof(TRequest).Name, string.Join(", ", requiredRoles));
                throw new UnauthorizedException($"One of the following roles is required: {string.Join(", ", requiredRoles)}");
            }
        }

        return await next();
    }
}
