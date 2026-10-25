using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace R2WAI.Infrastructure.Cache;

public class InMemoryCacheService : ICacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, CounterEntry> _counters = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly Timer _evictionTimer;

    public InMemoryCacheService(ILogger<InMemoryCacheService> logger)
    {
        _logger = logger;
        _evictionTimer = new Timer(_ => EvictExpired(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            _logger.LogTrace("Cache hit for key: {Key}", key);
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
        _logger.LogTrace("Cache set for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<(long Count, TimeSpan Remaining)?> IncrementWithExpiryAsync(
        string key, TimeSpan expiration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var entry = _counters.GetOrAdd(key, _ => new CounterEntry(expiration));
        return Task.FromResult<(long Count, TimeSpan Remaining)?>(entry.Increment(expiration));
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _cache.TryRemove(key, out _);
        _counters.TryRemove(key, out _);
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

    private void EvictExpired()
    {
        var count = 0;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.IsExpired && _cache.TryRemove(kvp.Key, out _))
                count++;
        }
        foreach (var kvp in _counters)
        {
            if (kvp.Value.IsExpired && _counters.TryRemove(kvp.Key, out _))
                count++;
        }
        if (count > 0)
            _logger.LogDebug("Evicted {Count} expired cache entries", count);
    }

    public void Dispose()
    {
        _evictionTimer.Dispose();
        GC.SuppressFinalize(this);
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

    private sealed class CounterEntry
    {
        private readonly object _gate = new();
        private long _count;
        private DateTime _expiresAt;

        public bool IsExpired
        {
            get
            {
                lock (_gate)
                    return DateTime.UtcNow >= _expiresAt;
            }
        }

        public CounterEntry(TimeSpan expiration) => _expiresAt = DateTime.UtcNow.Add(expiration);

        public (long Count, TimeSpan Remaining) Increment(TimeSpan expiration)
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                if (now >= _expiresAt)
                {
                    _count = 0;
                    _expiresAt = now.Add(expiration);
                }

                _count++;
                return (_count, _expiresAt - now);
            }
        }
    }
}
