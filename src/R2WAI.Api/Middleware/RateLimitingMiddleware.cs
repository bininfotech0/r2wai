using System.Collections.Concurrent;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    // In-memory fallback — used only when Redis is unavailable
    private readonly ConcurrentDictionary<string, RateLimitEntry> _fallbackClients = new();

    public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 100, int windowSeconds = 60)
    {
        _next = next;
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientKey = GetClientKey(context);
        var cache = context.RequestServices.GetService<ICacheService>();

        int count;
        DateTime windowStart;

        if (cache is not null)
        {
            (count, windowStart) = await GetOrIncrementDistributed(cache, clientKey);
        }
        else
        {
            (count, windowStart) = GetOrIncrementLocal(clientKey);
        }

        context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, _maxRequests - count).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(windowStart + _window).ToUnixTimeSeconds().ToString();

        if (count > _maxRequests)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = ((int)(_window - (DateTime.UtcNow - windowStart)).TotalSeconds + 1).ToString();
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Please try again later." });
            return;
        }

        await _next(context);
    }

    private async Task<(int count, DateTime windowStart)> GetOrIncrementDistributed(ICacheService cache, string clientKey)
    {
        var cacheKey = $"ratelimit:{clientKey}";
        try
        {
            var entry = await cache.GetAsync<RateLimitData>(cacheKey);
            var now = DateTime.UtcNow;

            if (entry is null || now - entry.WindowStart > _window)
            {
                entry = new RateLimitData { WindowStart = now, Count = 1 };
                await cache.SetAsync(cacheKey, entry, _window);
                return (1, now);
            }

            entry.Count++;
            var remaining = _window - (now - entry.WindowStart);
            await cache.SetAsync(cacheKey, entry, remaining > TimeSpan.Zero ? remaining : _window);
            return (entry.Count, entry.WindowStart);
        }
        catch
        {
            return GetOrIncrementLocal(clientKey);
        }
    }

    private (int count, DateTime windowStart) GetOrIncrementLocal(string clientKey)
    {
        var now = DateTime.UtcNow;
        var entry = _fallbackClients.GetOrAdd(clientKey, _ => new RateLimitEntry(now));

        if (now - entry.WindowStart > _window)
            entry.Reset(now);

        return (entry.Increment(), entry.WindowStart);
    }

    private static string GetClientKey(HttpContext context)
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private class RateLimitData
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
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
