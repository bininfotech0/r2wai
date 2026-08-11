namespace R2WAI.Domain.Tests.Entities;

public class ApplicationVersionTests
{
    [Fact]
    public void CreateSnapshot_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var version = ApplicationVersion.CreateSnapshot(
            id, tenantId, applicationId, 1, "{\"name\":\"Property Tax\"}", "Initial snapshot");

        Assert.Equal(id, version.Id);
        Assert.Equal(tenantId, version.TenantId);
        Assert.Equal(applicationId, version.ApplicationId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("{\"name\":\"Property Tax\"}", version.ConfigSnapshot);
        Assert.Equal("Initial snapshot", version.Note);
        Assert.False(version.IsPublished);
        Assert.Null(version.PublishedAt);
    }

    [Fact]
    public void Publish_SetsPublishedStateAndTimestamp()
    {
        var version = ApplicationVersion.CreateSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "{}");
        var publishedBy = Guid.NewGuid();

        version.Publish(publishedBy);

        Assert.True(version.IsPublished);
        Assert.Equal(publishedBy, version.PublishedByUserId);
        Assert.NotNull(version.PublishedAt);
    }

    [Fact]
    public void Unpublish_ClearsPublishedFlag()
    {
        var version = ApplicationVersion.CreateSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "{}");
        version.Publish(Guid.NewGuid());

        version.Unpublish();

        Assert.False(version.IsPublished);
    }
}
