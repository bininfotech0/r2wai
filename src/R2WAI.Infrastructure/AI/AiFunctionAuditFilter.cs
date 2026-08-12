using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Logs every Semantic Kernel function the model auto-invokes, so autonomous
/// tool calls (workflow starts, approvals, notifications) leave an audit trail,
/// and records the same calls into the scoped IChatTraceCollector so a single
/// chat turn's real execution trace can be surfaced (Test Studio).
/// </summary>
public class AiFunctionAuditFilter : IFunctionInvocationFilter
{
    private readonly ILogger<AiFunctionAuditFilter> _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatTraceCollector _traceCollector;

    public AiFunctionAuditFilter(ILogger<AiFunctionAuditFilter> logger, ICurrentUserService currentUser, IChatTraceCollector traceCollector)
    {
        _logger = logger;
        _currentUser = currentUser;
        _traceCollector = traceCollector;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var plugin = context.Function.PluginName ?? "unknown";
        var function = context.Function.Name;
        var arguments = context.Arguments.Count > 0 ? string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")) : null;

        _logger.LogInformation(
            "AI invoked function {Plugin}.{Function} with args {Arguments} (User {UserId}, Tenant {TenantId})",
            plugin, function, context.Arguments, _currentUser.UserId, _currentUser.TenantId);

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
            sw.Stop();
            _traceCollector.RecordFunctionCall(plugin, function, arguments, sw.ElapsedMilliseconds, success: true, error: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _traceCollector.RecordFunctionCall(plugin, function, arguments, sw.ElapsedMilliseconds, success: false, error: ex.Message);
            throw;
        }

        _logger.LogInformation(
            "AI function {Plugin}.{Function} completed", plugin, function);
    }
}
