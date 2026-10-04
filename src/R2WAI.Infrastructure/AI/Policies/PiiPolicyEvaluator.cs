using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Pure parsing logic behind the "Pii" GlobalPolicy's optional structured action. Mirrors
/// ToolExecutionPolicyEvaluator/AiUsagePolicyEvaluator exactly: GlobalPolicy.Content is free-text
/// prose by default (no enforcement, current behavior unchanged), and an admin who instead puts
/// {"action":"redact"} or {"action":"block"} there opts into real enforcement.
/// </summary>
public static class PiiPolicyEvaluator
{
    /// <summary>
    /// Returns "redact" or "block" (case-insensitive input, normalized to lowercase) for a
    /// recognized structured action, or null for prose content, empty content, unparseable JSON,
    /// or an unrecognized action value — all of which mean "no enforcement configured".
    /// </summary>
    public static string? TryParseAction(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("action", out var prop)
                || prop.ValueKind != JsonValueKind.String)
                return null;

            var action = prop.GetString()?.ToLowerInvariant();
            return action is "redact" or "block" ? action : null;
        }
        catch (JsonException)
        {
            // Prose policy text — the normal/expected case. No enforcement change.
            return null;
        }
    }
}
