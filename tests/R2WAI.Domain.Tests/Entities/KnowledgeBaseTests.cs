namespace R2WAI.Domain.Tests.Entities;

public class KnowledgeBaseTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var kb = new KnowledgeBase(id, tenantId, userId, "My KB", "Test description");

        Assert.Equal(id, kb.Id);
        Assert.Equal("My KB", kb.Name);
        Assert.Equal("Test description", kb.Description);
        Assert.Equal(KnowledgeBaseStatus.Creating, kb.Status);
        Assert.Equal(0, kb.DocumentCount);
    }

    [Fact]
    public void UpdateStatus_ToActive_ChangesStatus()
    {
        var kb = CreateDefault();
        kb.UpdateStatus(KnowledgeBaseStatus.Active);
        Assert.Equal(KnowledgeBaseStatus.Active, kb.Status);
    }

    [Fact]
    public void IncrementDocumentCount_IncreasesCount()
    {
        var kb = CreateDefault();
        kb.IncrementDocumentCount();
        kb.IncrementDocumentCount();
        Assert.Equal(2, kb.DocumentCount);
    }

    [Fact]
    public void ConfigureEmbedding_SetsAllProperties()
    {
        var kb = CreateDefault();
        kb.ConfigureEmbedding("text-embedding-3-small", 512, 50, "kb_collection");

        Assert.Equal("text-embedding-3-small", kb.EmbeddingModel);
        Assert.Equal(512, kb.ChunkSize);
        Assert.Equal(50, kb.ChunkOverlap);
        Assert.Equal("kb_collection", kb.VectorCollectionName);
    }

    [Fact]
    public void UpdateDetails_ChangesNameAndDescription()
    {
        var kb = CreateDefault();
        kb.UpdateDetails("Updated Name", "Updated Description");
        Assert.Equal("Updated Name", kb.Name);
        Assert.Equal("Updated Description", kb.Description);
    }

    [Fact]
    public void AddSource_AddsToCollection()
    {
        var kb = CreateDefault();
        var source = new KnowledgeBaseSource(Guid.NewGuid(), kb.Id, "Text", content: "Some text");
        kb.AddSource(source);
        Assert.Single(kb.Sources);
    }

    [Fact]
    public void RemoveSource_RemovesFromCollection()
    {
        var kb = CreateDefault();
        var source = new KnowledgeBaseSource(Guid.NewGuid(), kb.Id, "Text", content: "Some text");
        kb.AddSource(source);
        kb.RemoveSource(source);
        Assert.Empty(kb.Sources);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var kb = CreateDefault();
        kb.SoftDelete();
        Assert.True(kb.IsDeleted);
    }

    private static KnowledgeBase CreateDefault()
    {
        return new KnowledgeBase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test KB");
    }
}
