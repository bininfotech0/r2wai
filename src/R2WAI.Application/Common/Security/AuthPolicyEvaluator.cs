using System.Text.Json;

namespace R2WAI.Application.Common.Security;

/// <summary>
/// Pure parsing logic behind the "Auth" GlobalPolicy's optional MFA floor. Mirrors
/// ApprovalPolicyEvaluator/ToolExecutionPolicyEvaluator's shape exactly: GlobalPolicy.Content is
/// free prose by default (no enforcement change), and an admin who instead puts a small JSON
/// object there, e.g. {"requireMfa":true}, opts into a real floor — any user without MFA enrolled
/// is blocked from completing login until they enroll.
/// </summary>
public static class AuthPolicyEvaluator
{
    /// <summary>
    /// Extracts "requireMfa" from a GlobalPolicy's Content if it's valid JSON with that boolean
    /// property; returns false for prose content, empty content, or JSON without that property —
    /// all of which mean "no floor configured", matching pre-Policy-Engine behavior (MFA stays
    /// opt-in per user).
    /// </summary>
    public static bool TryParseRequireMfa(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("requireMfa", out var prop)
                && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return prop.GetBoolean();
        }
        catch (JsonException)
        {
            // Not JSON — this is prose policy text, the normal/expected case. No enforcement change.
        }

        return false;
    }

    /// <summary>
    /// Extracts "maxPasswordAgeDays" (a positive integer) from the same "Auth" GlobalPolicy's
    /// Content, independent of requireMfa — a tenant can configure either, both, or neither.
    /// Returns null for prose content, empty content, JSON without that property, or a
    /// non-positive value (matching pre-Policy-Engine behavior: no expiry enforced).
    /// </summary>
    public static int? TryParseMaxPasswordAgeDays(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("maxPasswordAgeDays", out var prop)
                && prop.ValueKind == JsonValueKind.Number
                && prop.TryGetInt32(out var days)
                && days > 0)
                return days;
        }
        catch (JsonException)
        {
            // Not JSON — prose policy text, the normal/expected case. No enforcement change.
        }

        return null;
    }
}
