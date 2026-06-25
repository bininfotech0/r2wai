namespace R2WAI.Application.Common.Interfaces;

public interface IIdempotencyStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
