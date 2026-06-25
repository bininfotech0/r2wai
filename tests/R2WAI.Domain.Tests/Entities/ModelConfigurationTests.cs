namespace R2WAI.Domain.Tests.Entities;

public class ModelConfigurationTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var config = new ModelConfiguration(id, tenantId, "GPT-4o", "openai", "gpt-4o",
            "encrypted-key", "https://api.openai.com/v1");

        Assert.Equal(id, config.Id);
        Assert.Equal(tenantId, config.TenantId);
        Assert.Equal("GPT-4o", config.Name);
        Assert.Equal("openai", config.Provider);
        Assert.Equal("gpt-4o", config.ModelId);
        Assert.Equal("encrypted-key", config.ApiKeyEncrypted);
        Assert.Equal("https://api.openai.com/v1", config.Endpoint);
        Assert.True(config.IsActive);
        Assert.False(config.IsDefault);
    }

    [Fact]
    public void Create_WithMinimalData_DefaultsCorrectly()
    {
        var config = new ModelConfiguration(Guid.NewGuid(), Guid.NewGuid(),
            "Ollama Local", "ollama", "qwen2.5-coder:7b");

        Assert.Null(config.ApiKeyEncrypted);
        Assert.Null(config.Endpoint);
        Assert.Null(config.MaxTokens);
        Assert.Null(config.Temperature);
        Assert.Null(config.TopP);
        Assert.True(config.IsActive);
        Assert.False(config.IsDefault);
    }

    [Fact]
    public void UpdateDetails_ChangesAllFields()
    {
        var config = CreateDefault();

        config.UpdateDetails("Updated GPT-4", "azure-openai", "gpt-4-turbo",
            4096, 0.7, 0.95, "https://my-instance.openai.azure.com");

        Assert.Equal("Updated GPT-4", config.Name);
        Assert.Equal("azure-openai", config.Provider);
        Assert.Equal("gpt-4-turbo", config.ModelId);
        Assert.Equal(4096, config.MaxTokens);
        Assert.Equal(0.7, config.Temperature);
        Assert.Equal(0.95, config.TopP);
        Assert.Equal("https://my-instance.openai.azure.com", config.Endpoint);
        Assert.NotNull(config.ModifiedAt);
    }

    [Fact]
    public void SetApiKey_UpdatesEncryptedKey()
    {
        var config = CreateDefault();

        config.SetApiKey("new-encrypted-key-xyz");

        Assert.Equal("new-encrypted-key-xyz", config.ApiKeyEncrypted);
        Assert.NotNull(config.ModifiedAt);
    }

    [Fact]
    public void SetDefault_ToTrue_SetsIsDefault()
    {
        var config = CreateDefault();
        Assert.False(config.IsDefault);

        config.SetDefault(true);

        Assert.True(config.IsDefault);
        Assert.NotNull(config.ModifiedAt);
    }

    [Fact]
    public void SetDefault_ToFalse_ClearsIsDefault()
    {
        var config = CreateDefault();
        config.SetDefault(true);

        config.SetDefault(false);

        Assert.False(config.IsDefault);
    }

    [Fact]
    public void Activate_SetsIsActive()
    {
        var config = CreateDefault();
        config.Deactivate();
        Assert.False(config.IsActive);

        config.Activate();

        Assert.True(config.IsActive);
    }

    [Fact]
    public void Deactivate_ClearsIsActive()
    {
        var config = CreateDefault();
        Assert.True(config.IsActive);

        config.Deactivate();

        Assert.False(config.IsActive);
        Assert.NotNull(config.ModifiedAt);
    }

    [Theory]
    [InlineData("openai", "gpt-4o")]
    [InlineData("ollama", "qwen2.5-coder:7b")]
    [InlineData("azure-openai", "gpt-4-turbo")]
    [InlineData("anthropic", "claude-sonnet-4-6")]
    public void Create_WithDifferentProviders_Works(string provider, string modelId)
    {
        var config = new ModelConfiguration(Guid.NewGuid(), Guid.NewGuid(),
            $"{provider} Model", provider, modelId);

        Assert.Equal(provider, config.Provider);
        Assert.Equal(modelId, config.ModelId);
    }

    private static ModelConfiguration CreateDefault()
    {
        return new ModelConfiguration(Guid.NewGuid(), Guid.NewGuid(),
            "Test Model", "openai", "gpt-4o", "test-key");
    }
}
