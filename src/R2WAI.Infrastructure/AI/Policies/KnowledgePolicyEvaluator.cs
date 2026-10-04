using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Pure parsing/comparison logic behind the "Knowledge" GlobalPolicy's optional data-classification
/// ceiling. Mirrors ToolExecutionPolicyEvaluator's shape exactly: GlobalPolicy.Content is free prose by
/// default (no enforcement change), and an admin who instead puts a small JSON object there, e.g.
/// {"maxClassification":"Internal"}, opts into a real ceiling — any KnowledgeBase whose
/// DataClassification ranks above it is excluded from RAG context, on top of (never instead of) the
/// underlying search itself. Kept free of EF/DB types so it's directly unit-testable.
/// </summary>
public static class KnowledgePolicyEvaluator
{
    private static readonly Dictionary<string, int> ClassificationRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Public"] = 0,
        ["Internal"] = 1,
        ["Confidential"] = 2,
        ["Restricted"] = 3
    };

    /// <summary>
    /// Extracts "maxClassification" from a GlobalPolicy's Content if it's valid JSON with that
    /// property; returns null for prose content, empty content, or JSON without that property — all of
    /// which mean "no ceiling configured", matching pre-Policy-Engine behavior.
    /// </summary>
    public static string? TryParseMaxClassification(string? policyContent)
    {
        if (string.IsNullOrWhiteSpace(policyContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(policyContent);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("maxClassification", out var prop)
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
    /// True only when both classifications are recognized (Public/Internal/Confidential/Restricted) and
    /// the knowledge base's level ranks strictly above the ceiling. An unrecognized label on either side
    /// never blocks — a typo in policy content should not accidentally deny every knowledge base.
    /// </summary>
    public static bool ExceedsCeiling(string kbClassification, string? maxClassification)
    {
        if (string.IsNullOrWhiteSpace(maxClassification))
            return false;

        if (!ClassificationRank.TryGetValue(kbClassification, out var kbRank))
            return false;

        if (!ClassificationRank.TryGetValue(maxClassification, out var ceilingRank))
            return false;

        return kbRank > ceilingRank;
    }
}
