namespace R2WAI.Domain.Tests.Entities;

public class ApplicationApiTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var api = new ApplicationApi(
            id, tenantId, applicationId, "Public API", "https://tax.example.gov/api",
            ApiAuthScheme.OAuth2, "secret-ref", "https://tax.example.gov/swagger.json");

        Assert.Equal(id, api.Id);
        Assert.Equal(tenantId, api.TenantId);
        Assert.Equal(applicationId, api.ApplicationId);
        Assert.Equal("Public API", api.Name);
        Assert.Equal("https://tax.example.gov/api", api.BaseUrl);
        Assert.Equal(ApiAuthScheme.OAuth2, api.AuthScheme);
        Assert.Equal("secret-ref", api.CredentialRef);
        Assert.Equal("https://tax.example.gov/swagger.json", api.OpenApiSource);
        Assert.True(api.IsActive);
    }

    [Fact]
    public void UpdateDetails_ChangesFields()
    {
        var api = new ApplicationApi(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Public API", "https://old.example.gov");

        api.UpdateDetails("Internal API", "https://new.example.gov", ApiAuthScheme.Jwt, "ref-2", null);

        Assert.Equal("Internal API", api.Name);
        Assert.Equal("https://new.example.gov", api.BaseUrl);
        Assert.Equal(ApiAuthScheme.Jwt, api.AuthScheme);
        Assert.Equal("ref-2", api.CredentialRef);
        Assert.Null(api.OpenApiSource);
        Assert.NotNull(api.ModifiedAt);
    }

    [Fact]
    public void Deactivate_Then_Activate_TogglesIsActive()
    {
        var api = new ApplicationApi(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Public API", "https://tax.example.gov");

        api.Deactivate();
        Assert.False(api.IsActive);

        api.Activate();
        Assert.True(api.IsActive);
    }
}
