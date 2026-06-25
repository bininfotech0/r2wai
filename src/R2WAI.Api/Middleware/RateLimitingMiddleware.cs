using System.Collections.Concurrent;

namespace R2WAI.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 100, int windowSeconds = 60)
    {
        _next = next;
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientKey = GetClientKey(context);
        var now = DateTime.UtcNow;

        var entry = _clients.GetOrAdd(clientKey, _ => new RateLimitEntry(now));

        if (now - entry.WindowStart > _window)
        {
            entry.Reset(now);
        }

        var count = entry.Increment();

        context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, _maxRequests - count).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(entry.WindowStart + _window).ToUnixTimeSeconds().ToString();

        if (count > _maxRequests)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = ((int)(_window - (now - entry.WindowStart)).TotalSeconds + 1).ToString();
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Please try again later." });
            return;
        }

        await _next(context);
    }

    private static string GetClientKey(HttpContext context)
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart;
        private int _count;

        public RateLimitEntry(DateTime windowStart)
        {
            WindowStart = windowStart;
            _count = 0;
        }

        public int Increment() => Interlocked.Increment(ref _count);

        public void Reset(DateTime newWindowStart)
        {
            WindowStart = newWindowStart;
            Interlocked.Exchange(ref _count, 0);
        }
    }
}
