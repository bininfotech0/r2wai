using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Pure parsing logic behind the "DataRetention" GlobalPolicy's optional retention window. Same
/// shape as the other Policy Engine evaluators: prose content (the default) means no purge job runs
/// for that tenant; {"retentionDays":N} opts into a real sweep.
/// </summary>
public static class DataRetentionPolicyEvaluator
{
    public static int? TryParseRetentionDays(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("retentionDays", out var prop)
                && prop.ValueKind == JsonValueKind.Number
                && prop.TryGetInt32(out var value)
                && value > 0)
                return value;
        }
        catch (JsonException)
        {
            // Not JSON — prose policy text, the normal/expected case. No purge job runs.
        }

        return null;
    }
}
