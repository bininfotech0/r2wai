namespace R2WAI.Domain.Tests.Entities;

public class ApplicationConfigurationTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var configuration = new ApplicationConfiguration(id, tenantId, applicationId);

        Assert.Equal(id, configuration.Id);
        Assert.Equal(tenantId, configuration.TenantId);
        Assert.Equal(applicationId, configuration.ApplicationId);
        Assert.Equal(30, configuration.TimeoutSeconds);
        Assert.Equal(3, configuration.MaxRetries);
        Assert.Equal(0.7, configuration.RagThreshold);
        Assert.Null(configuration.ModelId);
        Assert.Null(configuration.SystemPromptTemplate);
    }

    [Fact]
    public void UpdateSettings_ChangesFields()
    {
        var configuration = new ApplicationConfiguration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        configuration.UpdateSettings(60, 5, 0.85, "gpt-4o", "You are a helpful assistant.");

        Assert.Equal(60, configuration.TimeoutSeconds);
        Assert.Equal(5, configuration.MaxRetries);
        Assert.Equal(0.85, configuration.RagThreshold);
        Assert.Equal("gpt-4o", configuration.ModelId);
        Assert.Equal("You are a helpful assistant.", configuration.SystemPromptTemplate);
        Assert.NotNull(configuration.ModifiedAt);
    }
}
