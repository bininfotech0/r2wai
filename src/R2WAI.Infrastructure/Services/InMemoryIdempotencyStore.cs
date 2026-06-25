using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public class InMemoryIdempotencyStore(ILogger<InMemoryIdempotencyStore> logger) : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (object Value, DateTime ExpiresAt)> _cache = new();

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            logger.LogDebug("IdempotencyStore hit for key: {Key}", key);
            return Task.FromResult(entry.Value as T);
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
            _cache.TryRemove(key, out _);

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        var expiry = DateTime.UtcNow.Add(ttl ?? DefaultTtl);
        _cache[key] = (value, expiry);
        logger.LogDebug("IdempotencyStore set for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult(true);
            _cache.TryRemove(key, out _);
        }

        return Task.FromResult(false);
    }
}
