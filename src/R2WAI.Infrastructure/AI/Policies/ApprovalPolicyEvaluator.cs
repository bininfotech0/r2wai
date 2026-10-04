using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Pure parsing logic behind the "Approval" GlobalPolicy's optional risk floor. Mirrors
/// ToolExecutionPolicyEvaluator's shape exactly: GlobalPolicy.Content is free prose by default (no
/// enforcement change), and an admin who instead puts a small JSON object there, e.g.
/// {"requireApprovalAboveRiskLevel":"Medium"}, opts into a real floor — any tool whose
/// ToolDefinition.RiskLevel exceeds it is treated as requiring approval (the same
/// GovernanceDecision.DenyApprovalRequired outcome ToolDefinition.ApprovalRequired already produces in
/// AiFunctionAuditFilter), on top of (never instead of) that per-tool flag and the separate
/// "ToolExecution" hard ceiling. The rank comparison itself is identical to ToolExecution's, so this
/// deliberately reuses ToolExecutionPolicyEvaluator.ExceedsCeiling rather than duplicating the
/// Low/Medium/High rank table.
/// </summary>
public static class ApprovalPolicyEvaluator
{
    /// <summary>
    /// Extracts "requireApprovalAboveRiskLevel" from a GlobalPolicy's Content if it's valid JSON with
    /// that property; returns null for prose content, empty content, or JSON without that property —
    /// all of which mean "no floor configured", matching pre-Policy-Engine behavior.
    /// </summary>
    public static string? TryParseRequireApprovalAboveRiskLevel(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("requireApprovalAboveRiskLevel", out var prop)
                && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        catch (JsonException)
        {
            // Not JSON — this is prose policy text, the normal/expected case. No enforcement change.
        }

        return null;
    }
}
