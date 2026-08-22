using System.Text.Json;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Pure parsing/comparison logic behind the "ToolExecution" GlobalPolicy's optional risk ceiling.
/// GlobalPolicy.Content is a free-text field (its UI placeholder literally reads "Describe the rule
/// this policy enforces or documents...") — most tenants will leave it as prose, and that must keep
/// working exactly as before (no enforcement). An admin who instead puts a small JSON object there,
/// e.g. {"maxRiskLevel":"Medium"}, opts into a real ceiling: any tool whose ToolDefinition.RiskLevel
/// exceeds it is denied, on top of (never instead of) the existing RequiredRole/ApprovalRequired
/// checks in AiFunctionAuditFilter.EvaluateGovernance. Kept free of EF/DB types so it's directly
/// unit-testable.
/// </summary>
public static class ToolExecutionPolicyEvaluator
{
    private static readonly Dictionary<string, int> RiskRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 0,
        ["Medium"] = 1,
        ["High"] = 2
    };

    /// <summary>
    /// Extracts "maxRiskLevel" from a GlobalPolicy's Content if it's valid JSON with that property;
    /// returns null for prose content, empty content, or JSON without that property — all of which
    /// mean "no ceiling configured", matching pre-Policy-Engine behavior.
    /// </summary>
    public static string? TryParseMaxRiskLevel(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("maxRiskLevel", out var prop)
                && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        catch (JsonException)
        {
            // Not JSON — this is prose policy text, the normal/expected case. No enforcement change.
        }

        return null;
    }

    /// <summary>
    /// True only when both risk levels are recognized (Low/Medium/High) and the tool's level ranks
    /// strictly above the ceiling. An unrecognized label on either side never blocks — a typo in
    /// policy content should not accidentally deny every tool call.
    /// </summary>
    public static bool ExceedsCeiling(string toolRiskLevel, string? maxRiskLevel)
    {
        if (string.IsNullOrWhiteSpace(maxRiskLevel))
            return false;

        if (!RiskRank.TryGetValue(toolRiskLevel, out var toolRank))
            return false;

        if (!RiskRank.TryGetValue(maxRiskLevel, out var ceilingRank))
            return false;

        return toolRank > ceilingRank;
    }
}
