using StackExchange.Redis;
using System.Text.Json;

namespace R2WAI.Infrastructure.Cache;

public class RedisCacheService : ICacheService, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConfiguration configuration,
        ILogger<RedisCacheService> logger)
    {
        _logger = logger;
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
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        _redis = ConnectionMultiplexer.Connect(options);
        _database = _redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = await _database.StringGetAsync(key);
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
            await _database.StringSetAsync(key, data, expiration ?? TimeSpan.FromMinutes(30));
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
            await _database.KeyDeleteAsync(key);
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
            return await _database.KeyExistsAsync(key);
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
