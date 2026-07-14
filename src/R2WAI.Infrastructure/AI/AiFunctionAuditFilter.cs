using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Logs every Semantic Kernel function the model auto-invokes, so autonomous
/// tool calls (workflow starts, approvals, notifications) leave an audit trail.
/// </summary>
public class AiFunctionAuditFilter : IFunctionInvocationFilter
{
    private readonly ILogger<AiFunctionAuditFilter> _logger;
    private readonly ICurrentUserService _currentUser;

    public AiFunctionAuditFilter(ILogger<AiFunctionAuditFilter> logger, ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        _logger.LogInformation(
            "AI invoked function {Plugin}.{Function} with args {Arguments} (User {UserId}, Tenant {TenantId})",
            context.Function.PluginName, context.Function.Name, context.Arguments, _currentUser.UserId, _currentUser.TenantId);

        await next(context);

        _logger.LogInformation(
            "AI function {Plugin}.{Function} completed", context.Function.PluginName, context.Function.Name);
    }
}
