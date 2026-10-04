namespace R2WAI.Application.Features.Assistants.Commands;

/// <summary>
/// GetAssistantsQuery caches its result keyed by the exact (page, pageSize)
/// requested. Create/Update/Delete must invalidate every (page, pageSize)
/// combination actually queried elsewhere in the app, or a mutation leaves
/// stale results behind for the cache's 2-minute TTL. Confirmed live: a
/// pageSize=100 assistant picker (used by Chatbot/TestCase/Application
/// dialogs) kept showing deleted assistants and omitting newly-created ones
/// because only pageSize=20 was ever invalidated here.
/// </summary>
internal static class AssistantCacheKeys
{
    private static readonly int[] PageSizes = [20, 50, 100, 200];

    public static Task InvalidateAsync(ICacheService cache, Guid tenantId, CancellationToken ct)
    {
        // Fired concurrently, not awaited one at a time: these are independent keys, and a slow or
        // unreachable cache backend (each RemoveAsync bounded by its own timeout, not free) would
        // otherwise multiply that latency by every (page, pageSize) combination instead of paying
        // it once.
        var removals = PageSizes.SelectMany(pageSize => Enumerable.Range(1, 5)
            .Select(page => cache.RemoveAsync($"assistants:{tenantId}:p{page}:s{pageSize}", ct)));
        return Task.WhenAll(removals);
    }
}
