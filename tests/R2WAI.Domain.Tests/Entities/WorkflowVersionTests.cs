namespace R2WAI.Domain.Tests.Entities;

public class WorkflowVersionTests
{
    [Fact]
    public void CreateSnapshot_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();

        var version = WorkflowVersion.CreateSnapshot(
            id, tenantId, workflowId, 1, "{\"Name\":\"Invoice Approval\"}");

        Assert.Equal(id, version.Id);
        Assert.Equal(tenantId, version.TenantId);
        Assert.Equal(workflowId, version.WorkflowId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("{\"Name\":\"Invoice Approval\"}", version.ConfigSnapshot);
        Assert.False(version.IsPublished);
        Assert.Null(version.PublishedAt);
    }

    [Fact]
    public void Publish_SetsPublishedStateAndTimestamp()
    {
        var version = WorkflowVersion.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "{}");
        var publishedBy = Guid.NewGuid();

        version.Publish(publishedBy);

        Assert.True(version.IsPublished);
        Assert.Equal(publishedBy, version.PublishedByUserId);
        Assert.NotNull(version.PublishedAt);
    }

    [Fact]
    public void Unpublish_ClearsPublishedFlag()
    {
        var version = WorkflowVersion.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "{}");
        version.Publish(Guid.NewGuid());

        version.Unpublish();

        Assert.False(version.IsPublished);
    }
}
