using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// Per-application runtime settings for a <see cref="ConnectedApplication"/>. One-to-one with the application.
/// </summary>
public sealed class ApplicationConfiguration : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int TimeoutSeconds { get; private set; } = 30;
    public int MaxRetries { get; private set; } = 3;
    public double RagThreshold { get; private set; } = 0.7;
    public string? ModelId { get; private set; }
    public string? SystemPromptTemplate { get; private set; }

    public ConnectedApplication Application { get; private set; } = null!;

    private ApplicationConfiguration() { }

    public ApplicationConfiguration(Guid id, Guid tenantId, Guid applicationId)
    {
        Id = id;
        TenantId = tenantId;
        ApplicationId = applicationId;
        TimeoutSeconds = 30;
        MaxRetries = 3;
        RagThreshold = 0.7;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateSettings(int timeoutSeconds, int maxRetries, double ragThreshold,
                               string? modelId, string? systemPromptTemplate)
    {
        TimeoutSeconds = timeoutSeconds;
        MaxRetries = maxRetries;
        RagThreshold = ragThreshold;
        ModelId = modelId;
        SystemPromptTemplate = systemPromptTemplate;
        MarkAsModified();
    }
}
