using System.Diagnostics;

namespace R2WAI.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    private static readonly HashSet<string> SensitiveHeaders =
        ["Authorization", "X-Api-Key", "Cookie", "Set-Cookie"];

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        catch (Exception)
        {
            stopwatch.Stop();
            _logger.LogError("HTTP {Method} {Path} failed after {Elapsed}ms",
                method, path, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
