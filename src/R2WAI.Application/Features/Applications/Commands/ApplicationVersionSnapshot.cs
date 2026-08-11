using System.Text.Json;

namespace R2WAI.Application.Features.Applications.Commands;

internal class ApplicationConfigSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BaseUrl { get; init; }
    public string Environment { get; init; } = nameof(ApplicationEnvironment.Development);
    public List<ApplicationApiSnapshotItem> Apis { get; init; } = [];
    public ApplicationConfigurationSnapshotItem? Configuration { get; init; }
}

internal class ApplicationApiSnapshotItem
{
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string AuthScheme { get; init; } = nameof(ApiAuthScheme.None);
    public string? CredentialRef { get; init; }
    public string? OpenApiSource { get; init; }
}

internal class ApplicationConfigurationSnapshotItem
{
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
    public double RagThreshold { get; init; } = 0.7;
    public string? ModelId { get; init; }
    public string? SystemPromptTemplate { get; init; }
}

/// <summary>
/// Builds the JSON snapshot stored on an <see cref="ApplicationVersion"/> and restores it back
/// onto a live application during rollback. Shared by <see cref="CreateApplicationVersionCommandHandler"/>
/// and <see cref="RollbackApplicationVersionCommandHandler"/> so both always snapshot the same shape.
/// </summary>
internal static class ApplicationVersionSnapshotService
{
    public static async Task<string> BuildSnapshotJsonAsync(
        ConnectedApplication application,
        IRepository<ApplicationApi> apiRepo,
        IRepository<ApplicationConfiguration> configRepo,
        CancellationToken cancellationToken)
    {
        var apis = await apiRepo.FindAsync(a => a.ApplicationId == application.Id, cancellationToken);
        var configuration = await configRepo.FirstOrDefaultAsync(c => c.ApplicationId == application.Id, cancellationToken);

        var snapshot = new ApplicationConfigSnapshot
        {
            Name = application.Name,
            Description = application.Description,
            BaseUrl = application.BaseUrl,
            Environment = application.Environment.ToString(),
            Apis = apis.Select(a => new ApplicationApiSnapshotItem
            {
                Name = a.Name,
                BaseUrl = a.BaseUrl,
                AuthScheme = a.AuthScheme.ToString(),
                CredentialRef = a.CredentialRef,
                OpenApiSource = a.OpenApiSource
            }).ToList(),
            Configuration = configuration is null ? null : new ApplicationConfigurationSnapshotItem
            {
                TimeoutSeconds = configuration.TimeoutSeconds,
                MaxRetries = configuration.MaxRetries,
                RagThreshold = configuration.RagThreshold,
                ModelId = configuration.ModelId,
                SystemPromptTemplate = configuration.SystemPromptTemplate
            }
        };

        return JsonSerializer.Serialize(snapshot);
    }

    public static ApplicationConfigSnapshot Deserialize(string configSnapshotJson) =>
        JsonSerializer.Deserialize<ApplicationConfigSnapshot>(configSnapshotJson)
            ?? throw new InvalidOperationException("Stored application version snapshot could not be read.");
}
