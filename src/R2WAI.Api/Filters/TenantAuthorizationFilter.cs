using Microsoft.AspNetCore.Mvc.Filters;
using R2WAI.Application.Common.Exceptions;

namespace R2WAI.Api.Filters;

public class TenantAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ILogger<TenantAuthorizationFilter> _logger;

    public TenantAuthorizationFilter(ILogger<TenantAuthorizationFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var tenantIdFromItems = context.HttpContext.Items["TenantId"] as Guid?;

        if (tenantIdFromItems is null)
        {
            var tenantIdFromHeader = context.HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantIdFromHeader) && Guid.TryParse(tenantIdFromHeader, out var parsedTenantId))
            {
                tenantIdFromItems = parsedTenantId;
                context.HttpContext.Items["TenantId"] = parsedTenantId;
            }
        }

        if (tenantIdFromItems is null)
        {
            _logger.LogWarning("Tenant not resolved for request {Path}", context.HttpContext.Request.Path);
            return;
        }

        var userTenantId = context.HttpContext.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(userTenantId) && Guid.TryParse(userTenantId, out var userTenant))
        {
            if (userTenant != tenantIdFromItems.Value)
            {
                _logger.LogWarning("User tenant {UserTenant} does not match request tenant {RequestTenant}",
                    userTenant, tenantIdFromItems.Value);
                throw new UnauthorizedException("You do not have access to this tenant's resources.");
            }
        }

        await Task.CompletedTask;
    }
}
