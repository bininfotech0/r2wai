using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace R2WAI.Api.Filters;

/// <summary>
/// SignalR hub methods run outside the ASP.NET Core request pipeline once the initial
/// WebSocket handshake completes, so <see cref="IHttpContextAccessor.HttpContext"/> is
/// null during hub invocations even though the connection is authenticated. Anything
/// backed by IHttpContextAccessor (e.g. ICurrentUserService, used by AI plugins like
/// WorkflowPlugin for tenant/user resolution) silently sees no user unless we restore
/// the context here for the duration of each invocation.
/// </summary>
public class HttpContextPropagationHubFilter : IHubFilter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextPropagationHubFilter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        _httpContextAccessor.HttpContext = invocationContext.Context.GetHttpContext();
        try
        {
            return await next(invocationContext);
        }
        finally
        {
            _httpContextAccessor.HttpContext = null;
        }
    }
}
