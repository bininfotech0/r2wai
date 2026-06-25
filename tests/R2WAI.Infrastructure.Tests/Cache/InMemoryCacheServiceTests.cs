using Microsoft.Extensions.Logging.Abstractions;

namespace R2WAI.Infrastructure.Tests.Cache;

public class InMemoryCacheServiceTests
{
    private static InMemoryCacheService CreateCache() => new(NullLogger<InMemoryCacheService>.Instance);

    public record TestItem(string Name, int Value);

    [Fact]
    public async Task SetAndGet_ReturnsValue()
    {
        var cache = CreateCache();
        await cache.SetAsync("key1", new TestItem("foo", 42));
        var result = await cache.GetAsync<TestItem>("key1");
        Assert.NotNull(result);
        Assert.Equal("foo", result!.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var cache = CreateCache();
        var result = await cache.GetAsync<TestItem>("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_ReturnsTrueForExistingKey()
    {
        var cache = CreateCache();
        await cache.SetAsync("k", new TestItem("x", 1));
        Assert.True(await cache.ExistsAsync("k"));
    }

    [Fact]
    public async Task Exists_ReturnsFalseForMissingKey()
    {
        var cache = CreateCache();
        Assert.False(await cache.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task Remove_DeletesKey()
    {
        var cache = CreateCache();
        await cache.SetAsync("r", new TestItem("rem", 1));
        await cache.RemoveAsync("r");
        Assert.False(await cache.ExistsAsync("r"));
    }

    [Fact]
    public async Task ExpiredEntry_ReturnsNull()
    {
        var cache = CreateCache();
        await cache.SetAsync("exp", new TestItem("e", 1), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        var result = await cache.GetAsync<TestItem>("exp");
        Assert.Null(result);
    }
}
