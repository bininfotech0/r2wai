using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace R2WAI.Infrastructure.Cache;

public class InMemoryCacheService(ILogger<InMemoryCacheService> logger) : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            logger.LogTrace("Cache hit for key: {Key}", key);
            return Task.FromResult(entry.Value as T);
        }

        if (entry?.IsExpired == true)
            _cache.TryRemove(key, out _);

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();
        _cache[key] = new CacheEntry(value, expiration ?? DefaultExpiration);
        logger.LogTrace("Cache set for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            return Task.FromResult(true);

        if (entry?.IsExpired == true)
            _cache.TryRemove(key, out _);

        return Task.FromResult(false);
    }

    private sealed class CacheEntry
    {
        public object Value { get; }
        public DateTime ExpiresAt { get; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        public CacheEntry(object value, TimeSpan ttl)
        {
            Value = value;
            ExpiresAt = DateTime.UtcNow.Add(ttl);
        }
    }
}
