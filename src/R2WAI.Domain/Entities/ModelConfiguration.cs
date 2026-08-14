using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class ModelConfiguration : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public string Name { get; private set; }
    public string Provider { get; private set; }
    public string ModelId { get; private set; }
    public string? ApiKeyEncrypted { get; private set; }
    public string? Endpoint { get; private set; }
    public int? MaxTokens { get; private set; }
    public double? Temperature { get; private set; }
    public double? TopP { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Governance — what sensitivity of data this model is approved to process, and which
    // department/application it's scoped to, if not tenant-wide.
    public DataClassification DataClassification { get; private set; } = DataClassification.Internal;

    public Tenant Tenant { get; private set; } = null!;
    public ConnectedApplication? Application { get; private set; }
    public Department? Department { get; private set; }

    private ModelConfiguration() { }

    public ModelConfiguration(Guid id, Guid tenantId, string name, string provider,
                               string modelId, string? apiKeyEncrypted = null,
                               string? endpoint = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Provider = provider;
        ModelId = modelId;
        ApiKeyEncrypted = apiKeyEncrypted;
        Endpoint = endpoint;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string provider, string modelId,
                               int? maxTokens, double? temperature, double? topP,
                               string? endpoint = null,
                               Guid? applicationId = null, Guid? departmentId = null,
                               DataClassification dataClassification = DataClassification.Internal)
    {
        Name = name;
        Provider = provider;
        ModelId = modelId;
        MaxTokens = maxTokens;
        Temperature = temperature;
        TopP = topP;
        Endpoint = endpoint;
        ApplicationId = applicationId;
        DepartmentId = departmentId;
        DataClassification = dataClassification;
        MarkAsModified();
    }

    public void SetApiKey(string apiKeyEncrypted)
    {
        ApiKeyEncrypted = apiKeyEncrypted;
        MarkAsModified();
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }
}
