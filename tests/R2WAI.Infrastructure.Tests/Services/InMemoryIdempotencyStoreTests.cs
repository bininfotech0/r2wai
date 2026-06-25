using Microsoft.Extensions.Logging.Abstractions;

namespace R2WAI.Infrastructure.Tests.Services;

public class InMemoryIdempotencyStoreTests
{
    private static InMemoryIdempotencyStore CreateStore() => new(NullLogger<InMemoryIdempotencyStore>.Instance);

    public record TestValue(string Name, int Count);

    [Fact]
    public async Task SetAndGet_ReturnsValue()
    {
        var store = CreateStore();
        var value = new TestValue("test", 42);

        await store.SetAsync("key1", value);
        var result = await store.GetAsync<TestValue>("key1");

        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
        Assert.Equal(42, result.Count);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var store = CreateStore();
        var result = await store.GetAsync<TestValue>("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_ReturnsTrueForExistingKey()
    {
        var store = CreateStore();
        await store.SetAsync("key2", new TestValue("exists", 1));

        var exists = await store.ExistsAsync("key2");
        Assert.True(exists);
    }

    [Fact]
    public async Task Exists_ReturnsFalseForMissingKey()
    {
        var store = CreateStore();
        var exists = await store.ExistsAsync("missing");
        Assert.False(exists);
    }

    [Fact]
    public async Task Set_DifferentKey_DoesNotOverwrite()
    {
        var store = CreateStore();
        await store.SetAsync("a", new TestValue("first", 1));
        await store.SetAsync("b", new TestValue("second", 2));

        var a = await store.GetAsync<TestValue>("a");
        var b = await store.GetAsync<TestValue>("b");

        Assert.Equal("first", a!.Name);
        Assert.Equal("second", b!.Name);
    }

    [Fact]
    public async Task Set_CustomTtl_ExpiresValue()
    {
        var store = CreateStore();
        await store.SetAsync("short", new TestValue("short-lived", 1), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var result = await store.GetAsync<TestValue>("short");
        Assert.Null(result);
    }
}
