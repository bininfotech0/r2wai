using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Pure parsing logic behind the "AiUsage" GlobalPolicy's optional daily request cap. Mirrors
/// ToolExecutionPolicyEvaluator's shape exactly: GlobalPolicy.Content is free prose by default (no
/// enforcement change for the common case), and an admin who puts a small JSON object there, e.g.
/// {"maxRequestsPerDay":100}, opts into a real cap on chat/message requests. Scoped to request-count
/// only, not tokens — TokensUsed isn't actually computed anywhere in this codebase yet
/// (ChatWithAssistantResult.TokensUsed is hardcoded to 0), so a token-based budget is a documented
/// follow-up once real token accounting exists, not something to fake here.
/// </summary>
public static class AiUsagePolicyEvaluator
{
    /// <summary>
    /// Extracts "maxRequestsPerDay" from a GlobalPolicy's Content if it's valid JSON with that
    /// property as a positive integer; returns null for prose content, empty content, JSON without
    /// that property, or a non-positive value — all of which mean "no cap configured".
    /// </summary>
    public static int? TryParseMaxRequestsPerDay(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("maxRequestsPerDay", out var prop)
                && prop.ValueKind == JsonValueKind.Number
                && prop.TryGetInt32(out var value)
                && value > 0)
                return value;
        }
        catch (JsonException)
        {
            // Not JSON — this is prose policy text, the normal/expected case. No enforcement change.
        }

        return null;
    }
}
