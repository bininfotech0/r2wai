using System.Security.Claims;
using R2WAI.Application.Common.Exceptions;

namespace R2WAI.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? tenantId = null;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            tenantId = context.User.FindFirst("tenant_id")?.Value;
        }

        if (string.IsNullOrEmpty(tenantId) && context.User.Identity?.IsAuthenticated == true)
        {
            tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        }

        if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var parsedTenantId))
        {
            context.Items["TenantId"] = parsedTenantId;
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogWarning("Authenticated request without valid tenant ID from user {UserId}",
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing tenant context" });
            return;
        }

        await _next(context);
    }
}
