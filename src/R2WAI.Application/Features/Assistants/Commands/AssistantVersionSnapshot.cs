using System.Text.Json;

namespace R2WAI.Application.Features.Assistants.Commands;

internal class AssistantConfigSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AssistantType Type { get; init; }
    public string? SystemPrompt { get; init; }
    public Guid? ModelConfigurationId { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public string? Tools { get; init; }
    public string? Settings { get; init; }
    public string? Tags { get; init; }
    public string? AvatarUrl { get; init; }
}

/// <summary>
/// Builds the JSON snapshot stored on an <see cref="AssistantVersion"/> and restores it back onto a
/// live <see cref="AssistantDefinition"/> during rollback. Shared by <see cref="CreateAssistantVersionCommandHandler"/>
/// and <see cref="RollbackAssistantVersionCommandHandler"/> so both always snapshot the same shape —
/// mirrors KnowledgeBaseVersionSnapshotService's role for KnowledgeBaseVersion. Type is captured for
/// record-keeping only: AssistantDefinition has no setter for it (never mutable after creation via
/// UpdateAssistantCommand either), so rollback does not attempt to restore it — there is nothing to
/// restore, the live value can never have diverged from what any past version recorded.
/// </summary>
internal static class AssistantVersionSnapshotService
{
    public static string BuildSnapshotJson(AssistantDefinition assistant)
    {
        var snapshot = new AssistantConfigSnapshot
        {
            Name = assistant.Name,
            Description = assistant.Description,
            Type = assistant.Type,
            SystemPrompt = assistant.SystemPrompt,
            ModelConfigurationId = assistant.ModelConfigurationId,
            KnowledgeBaseId = assistant.KnowledgeBaseId,
            Tools = assistant.Tools,
            Settings = assistant.Settings,
            Tags = assistant.Tags,
            AvatarUrl = assistant.AvatarUrl,
        };

        return JsonSerializer.Serialize(snapshot);
    }

    public static AssistantConfigSnapshot Deserialize(string configSnapshotJson) =>
        JsonSerializer.Deserialize<AssistantConfigSnapshot>(configSnapshotJson)
            ?? throw new InvalidOperationException("Stored assistant version snapshot could not be read.");

    /// <summary>Same null-means-all / parse-failure-means-all semantics as
    /// AssistantDefinition.GetEnabledToolIds(), applied to a snapshot's Tools string instead of the
    /// live entity's — used by ChatWithPublishedAssistantCommand, which has no AssistantDefinition
    /// instance to call the method on.</summary>
    public static IReadOnlyCollection<Guid>? ParseEnabledToolIds(string? tools)
    {
        if (tools is null) return null;
        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(tools);
            return ids ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Mirrors AssistantDefinition.GetBehaviorSettings() against a snapshot's Settings
    /// string instead of the live entity's.</summary>
    public static Domain.Entities.AssistantBehaviorSettings? ParseBehaviorSettings(string? settings)
    {
        if (string.IsNullOrWhiteSpace(settings)) return null;
        try
        {
            return JsonSerializer.Deserialize<Domain.Entities.AssistantBehaviorSettings>(settings,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
