using System.Diagnostics;
using System.Security.Claims;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IRequestMetricsStore _metricsStore;

    private static readonly HashSet<string> SensitiveHeaders =
        ["Authorization", "X-Api-Key", "Cookie", "Set-Cookie"];

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IRequestMetricsStore metricsStore)
    {
        _next = next;
        _logger = logger;
        _metricsStore = metricsStore;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;

        try
        {
            await _next(context);
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var elapsed = stopwatch.ElapsedMilliseconds;

            RecordMetric(context, path.Value ?? string.Empty, statusCode, elapsed);

            if (statusCode >= 500)
            {
                _logger.LogError("HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                    method, path, statusCode, elapsed);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning("HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                    method, path, statusCode, elapsed);
            }
            else
            {
                _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                    method, path, statusCode, elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // The client hung up (navigated away, closed the tab, or React Query aborted an
            // in-flight request when a component unmounted). This middleware runs INSIDE
            // ExceptionHandlingMiddleware, so it sees the exception first — if it recorded a
            // 500 here, every routine navigation would (a) log a spurious error and (b) land
            // in the metrics store as a server fault, dragging the dashboard's Success Rate KPI
            // down on a perfectly healthy API. The request was never served, so it must not
            // count as a failure at all.
            if (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request {Method} {Path} aborted by the client after {Elapsed}ms",
                    method, path, stopwatch.ElapsedMilliseconds);
                return;
            }

            RecordMetric(context, path.Value ?? string.Empty, 500, stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "HTTP {Method} {Path} failed after {Elapsed}ms",
                method, path, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private void RecordMetric(HttpContext context, string path, int statusCode, long elapsedMs)
    {
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return;

        Guid? tenantId = Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tid) ? tid : null;
        Guid? userId = Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
        var category = path.Contains("/chat", StringComparison.OrdinalIgnoreCase)
            ? RequestMetricCategory.Ai
            : RequestMetricCategory.Api;

        _metricsStore.Record(new RequestMetricEntry(DateTime.UtcNow, tenantId, userId, path, statusCode, elapsedMs, category));
    }
}
