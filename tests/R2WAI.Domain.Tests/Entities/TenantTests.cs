namespace R2WAI.Domain.Tests.Entities;

public class TenantTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenant = new Tenant(id, "Acme Corp", "acme-corp", "acme.com");

        Assert.Equal(id, tenant.Id);
        Assert.Equal("Acme Corp", tenant.Name);
        Assert.Equal("acme-corp", tenant.Slug);
        Assert.Equal("acme.com", tenant.Domain);
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Create_WithoutDomain_DefaultsToNull()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Test", "test");

        Assert.Null(tenant.Domain);
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void UpdateDetails_ChangesNameSlugDomain()
    {
        var tenant = CreateDefault();
        tenant.UpdateDetails("New Name", "new-slug", "newdomain.com");

        Assert.Equal("New Name", tenant.Name);
        Assert.Equal("new-slug", tenant.Slug);
        Assert.Equal("newdomain.com", tenant.Domain);
        Assert.NotNull(tenant.ModifiedAt);
    }

    [Fact]
    public void UpdateStatus_ChangesStatus()
    {
        var tenant = CreateDefault();
        tenant.UpdateStatus(TenantStatus.Suspended);

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.NotNull(tenant.ModifiedAt);
    }

    [Fact]
    public void UpdateFeatures_SetsFeatures()
    {
        var tenant = CreateDefault();
        tenant.UpdateFeatures("{\"chat\": true, \"workflows\": true}");

        Assert.Equal("{\"chat\": true, \"workflows\": true}", tenant.Features);
    }

    [Fact]
    public void UpdateSettings_SetsSettings()
    {
        var tenant = CreateDefault();
        tenant.UpdateSettings("{\"theme\": \"dark\"}");

        Assert.Equal("{\"theme\": \"dark\"}", tenant.Settings);
    }

    private static Tenant CreateDefault()
    {
        return new Tenant(Guid.NewGuid(), "Test Tenant", "test-tenant");
    }
}
