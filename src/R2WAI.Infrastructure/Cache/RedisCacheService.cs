using StackExchange.Redis;
using System.Text.Json;

namespace R2WAI.Infrastructure.Cache;

public class RedisCacheService : ICacheService, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _instanceName;

    public RedisCacheService(
        IConfiguration configuration,
        ILogger<RedisCacheService> logger)
    {
        _logger = logger;
        // Namespaces every key so this app's cache entries can't collide with another
        // app/environment sharing the same Redis instance — the setting existed in config but was
        // never actually applied to any key written or read here.
        _instanceName = configuration["Cache:Redis:InstanceName"] ?? string.Empty;
        var connectionString = configuration["Cache:Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        // AbortOnConnectFail = false: every read/write below already has its own try/catch and
        // falls back gracefully (RateLimitingMiddleware falls back to an in-memory limiter on any
        // cache exception). Without this, ConnectionMultiplexer.Connect() throws synchronously the
        // moment Redis is unreachable -- since this service is a singleton, that exception surfaces
        // on the *next* DI resolution too, so a Redis blip took down every request in the app
        // (confirmed live: this is exactly what was happening in the test environment, where no
        // Redis is running at all) instead of degrading to the local fallback as intended.
        //
        // ConnectTimeout/SyncTimeout: that earlier fix stopped the crash but left two separate
        // StackExchange.Redis timeouts unbounded -- confirmed live again, this time as requests
        // taking well over a minute in a process where Redis is configured but unreachable.
        // AbortOnConnectFail=false means Connect() itself returns quickly either way, but every
        // individual command issued afterward (Get/Set/Delete) still queues in a backlog for up to
        // SyncTimeout (defaults to 5000ms) hoping a connection appears before it gives up and
        // throws. A single request that touches several cache keys (e.g.
        // AssistantCacheKeys.InvalidateAsync's per-page-size fan-out) pays that ~5s penalty once
        // per key, sequentially -- 20 keys x 5s was the actual measured ~117s. Bounding both here
        // means "Redis isn't there" fails fast into the graceful fallback every call site already
        // has, instead of multiplying a per-command timeout across however many keys one request
        // happens to touch.
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 3000;
        options.ConnectRetry = 1;
        options.SyncTimeout = 1000;
        _redis = ConnectionMultiplexer.Connect(options);
        _database = _redis.GetDatabase();
    }

    private string Namespaced(string key) => _instanceName.Length > 0 ? $"{_instanceName}{key}" : key;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = await _database.StringGetAsync(Namespaced(key));
            if (!data.HasValue) return null;

            return JsonSerializer.Deserialize<T>((string)data!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache get failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(value);
            await _database.StringSetAsync(Namespaced(key), data, expiration ?? TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache set failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _database.KeyDeleteAsync(Namespaced(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache remove failed for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _database.KeyExistsAsync(Namespaced(key));
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _redis.Dispose();
        GC.SuppressFinalize(this);
    }
}
