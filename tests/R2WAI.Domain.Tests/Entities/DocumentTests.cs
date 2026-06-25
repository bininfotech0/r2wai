namespace R2WAI.Domain.Tests.Entities;

public class DocumentTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var doc = new Document(id, tenantId, userId, "test.pdf",
            DocumentType.PDF, "/tmp/test.pdf", 1024);

        Assert.Equal(id, doc.Id);
        Assert.Equal(tenantId, doc.TenantId);
        Assert.Equal("test.pdf", doc.Name);
        Assert.Equal(DocumentType.PDF, doc.FileType);
        Assert.Equal(1024, doc.FileSize);
        Assert.Equal(DocumentStatus.Uploading, doc.Status);
    }

    [Fact]
    public void Create_WithKnowledgeBase_SetsKnowledgeBaseId()
    {
        var kbId = Guid.NewGuid();
        var doc = CreateDefault(knowledgeBaseId: kbId);
        Assert.Equal(kbId, doc.KnowledgeBaseId);
    }

    [Fact]
    public void UpdateStatus_ToProcessing_ChangesStatus()
    {
        var doc = CreateDefault();
        doc.UpdateStatus(DocumentStatus.Processing);
        Assert.Equal(DocumentStatus.Processing, doc.Status);
    }

    [Fact]
    public void UpdateStatus_ToFailed_SetsError()
    {
        var doc = CreateDefault();
        doc.UpdateStatus(DocumentStatus.Failed, "Parse error");
        Assert.Equal(DocumentStatus.Failed, doc.Status);
        Assert.Equal("Parse error", doc.ProcessingError);
    }

    [Fact]
    public void SetPageCount_UpdatesPageCount()
    {
        var doc = CreateDefault();
        doc.SetPageCount(42);
        Assert.Equal(42, doc.PageCount);
    }

    [Fact]
    public void SetVectorIds_UpdatesVectorIds()
    {
        var doc = CreateDefault();
        doc.SetVectorIds("vec1,vec2,vec3");
        Assert.Equal("vec1,vec2,vec3", doc.VectorIds);
    }

    [Fact]
    public void SetMetadata_UpdatesMetadata()
    {
        var doc = CreateDefault();
        doc.SetMetadata("{\"author\":\"test\"}");
        Assert.Equal("{\"author\":\"test\"}", doc.Metadata);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var doc = CreateDefault();
        doc.SoftDelete();
        Assert.True(doc.IsDeleted);
    }

    private static Document CreateDefault(Guid? knowledgeBaseId = null)
    {
        return new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "test.pdf", DocumentType.PDF, "/tmp/test.pdf", 2048, knowledgeBaseId);
    }
}
