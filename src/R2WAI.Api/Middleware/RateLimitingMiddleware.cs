using System.Collections.Concurrent;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Middleware;

/// <summary>
/// Distributed request throttling. The API installs one IP-wide guard before authentication, then
/// a second policy-aware guard after JWT/API-key authentication so anonymous abuse and authenticated
/// per-user load have separate limits.
/// </summary>
public class RateLimitingMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly bool _useRoutePolicies;
    private readonly bool _preferAuthenticatedUser;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _fallbackClients = new();
    private readonly Timer _cleanupTimer;

    public RateLimitingMiddleware(
        RequestDelegate next,
        int maxRequests = 2000,
        int windowSeconds = 60,
        bool useRoutePolicies = false,
        bool preferAuthenticatedUser = false)
    {
        _next = next;
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
        _useRoutePolicies = useRoutePolicies;
        _preferAuthenticatedUser = preferAuthenticatedUser;
        _cleanupTimer = new Timer(_ => CleanupExpiredEntries(), null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var policy = _useRoutePolicies
            ? SelectPolicy(context)
            : new RateLimitPolicy("ip-global", _maxRequests, _window);
        var clientKey = GetClientKey(context, policy.Scope, _preferAuthenticatedUser);
        var cache = context.RequestServices.GetService<ICacheService>();

        long count;
        DateTime windowStart;
        if (cache is not null)
            (count, windowStart) = await GetOrIncrementDistributed(cache, clientKey, policy.Window, context.RequestAborted);
        else
            (count, windowStart) = GetOrIncrementLocal(clientKey, policy.Window);

        context.Response.Headers["X-RateLimit-Limit"] = policy.MaxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0L, policy.MaxRequests - count).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(windowStart + policy.Window).ToUnixTimeSeconds().ToString();

        if (count > policy.MaxRequests)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var retryAfter = Math.Max(1, (int)Math.Ceiling((policy.Window - (DateTime.UtcNow - windowStart)).TotalSeconds));
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Please try again later." });
            return;
        }

        await _next(context);
    }

    private async Task<(long count, DateTime windowStart)> GetOrIncrementDistributed(
        ICacheService cache, string clientKey, TimeSpan window, CancellationToken ct)
    {
        var cacheKey = $"ratelimit:v3:{clientKey}";
        var result = await cache.IncrementWithExpiryAsync(cacheKey, window, ct);
        if (result is null)
            return GetOrIncrementLocal(clientKey, window);

        var now = DateTime.UtcNow;
        var elapsed = window - result.Value.Remaining;
        return (result.Value.Count, now - elapsed);
    }

    private (long count, DateTime windowStart) GetOrIncrementLocal(string clientKey, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var entry = _fallbackClients.GetOrAdd(clientKey, _ => new RateLimitEntry(now));
        return entry.Increment(now, window);
    }

    private static string GetClientKey(HttpContext context, string scope, bool preferAuthenticatedUser)
    {
        var userId = preferAuthenticatedUser
            ? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;
        var clientId = !string.IsNullOrEmpty(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        return $"{scope}:{clientId}";
    }

    private static RateLimitPolicy SelectPolicy(HttpContext context)
    {
        var method = context.Request.Method;
        var parts = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length < 3 || !parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals("v1", StringComparison.OrdinalIgnoreCase))
            return new RateLimitPolicy("api-default", 300, TimeSpan.FromMinutes(1));

        var controller = parts[2].ToLowerInvariant();
        if (method == HttpMethods.Post && controller == "auth" && parts.Length >= 4
            && parts[3].ToLowerInvariant() is "login" or "forgot-password" or "request-access" or "reset-password")
            return new RateLimitPolicy($"auth:{parts[3].ToLowerInvariant()}", 10, TimeSpan.FromMinutes(1));

        if (method == HttpMethods.Post && controller == "chatbots" && parts.Length >= 5)
        {
            var targetId = parts[3].ToLowerInvariant();
            var action = parts[4].ToLowerInvariant();
            if (action == "messages" && parts.Length >= 6 && parts[5].Equals("attachment", StringComparison.OrdinalIgnoreCase))
                return new RateLimitPolicy($"chatbot-attachment:{targetId}", 10, TimeSpan.FromMinutes(1));
            if (action is "chat" or "webhook")
                return new RateLimitPolicy($"chatbot:{targetId}:{action}", 30, TimeSpan.FromMinutes(1));
        }

        if (method == HttpMethods.Post && controller == "assistants" && parts.Length >= 5
            && parts[4].Equals("chat", StringComparison.OrdinalIgnoreCase))
            return new RateLimitPolicy($"assistant-chat:{parts[3].ToLowerInvariant()}", 30, TimeSpan.FromMinutes(1));

        if (method == HttpMethods.Post && controller == "workflows" && parts.Length >= 4
            && parts[3].Equals("webhook", StringComparison.OrdinalIgnoreCase))
            return new RateLimitPolicy($"workflow-webhook:{(parts.Length > 4 ? parts[4].ToLowerInvariant() : "unknown")}", 60, TimeSpan.FromMinutes(1));

        if (method == HttpMethods.Post && controller == "documents" && parts.Length >= 5
            && parts[4].Equals("ask", StringComparison.OrdinalIgnoreCase))
            return new RateLimitPolicy($"document-ask:{parts[3].ToLowerInvariant()}", 30, TimeSpan.FromMinutes(1));

        return new RateLimitPolicy("api-default", 300, TimeSpan.FromMinutes(1));
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _fallbackClients)
            if (entry.Value.IsStale(now, _window * 2))
                _fallbackClients.TryRemove(entry.Key, out _);
    }

    private sealed record RateLimitPolicy(string Scope, int MaxRequests, TimeSpan Window);

    private sealed class RateLimitEntry(DateTime windowStart)
    {
        private readonly object _gate = new();
        private DateTime _windowStart = windowStart;
        private long _count;

        public (long Count, DateTime WindowStart) Increment(DateTime now, TimeSpan window)
        {
            lock (_gate)
            {
                if (now - _windowStart >= window)
                {
                    _windowStart = now;
                    _count = 0;
                }

                return (++_count, _windowStart);
            }
        }

        public bool IsStale(DateTime now, TimeSpan retention)
        {
            lock (_gate)
                return now - _windowStart > retention;
        }
    }

    public void Dispose() => _cleanupTimer.Dispose();
}
