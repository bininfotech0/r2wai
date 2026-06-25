namespace R2WAI.Domain.Tests.ValueObjects;

public class FileInfoTests
{
    private static Domain.ValueObjects.FileInfo MakeInfo(string name = "test.pdf", string type = "application/pdf", int size = 1024, string path = "/uploads/test.pdf")
        => new(name, type, size, path);

    [Fact]
    public void Create_SetsProperties()
    {
        var info = MakeInfo();

        Assert.Equal("test.pdf", info.FileName);
        Assert.Equal("application/pdf", info.ContentType);
        Assert.Equal(1024, info.FileSize);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = MakeInfo();
        var b = MakeInfo();

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = MakeInfo();
        var b = MakeInfo(name: "other.pdf", size: 2048);

        Assert.NotEqual(a, b);
    }
}
