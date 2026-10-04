using System.Text.RegularExpressions;

namespace R2WAI.Infrastructure.AI.Policies;

public record PiiMatch(string Type, string Value);

/// <summary>
/// Pure pattern-based detection for the personal-data shapes this platform's own domain already
/// treats as sensitive (see User.AadhaarNumberHash, the Aadhaar-login regex in AuthController, and
/// the Indian-mobile validation regex in UpdateProfileRequest handling) — Aadhaar, PAN, Indian
/// mobile numbers, and email addresses. Not a general-purpose PII library: scoped to what a
/// citizen-facing Indian government platform actually collects, so false positives stay rare.
/// No DB/EF dependency, directly unit-testable.
/// </summary>
public static class PiiDetector
{
    // 12 digits, optionally space- or hyphen-grouped in the conventional 4-4-4 Aadhaar display
    // format. Matches the same bare "12 digits" shape AuthController.Login already uses to detect
    // an Aadhaar-based login attempt.
    private static readonly Regex Aadhaar = new(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", RegexOptions.Compiled);

    // Indian PAN: 5 letters, 4 digits, 1 letter — a fixed, unambiguous format.
    private static readonly Regex Pan = new(@"\b[A-Za-z]{5}\d{4}[A-Za-z]\b", RegexOptions.Compiled);

    // Same shape as the mobile-number validation already applied elsewhere in this codebase.
    private static readonly Regex IndianMobile = new(@"\b(?:\+91[\s-]?)?[6-9]\d{9}\b", RegexOptions.Compiled);

    private static readonly Regex Email = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    private static readonly (string Type, Regex Pattern)[] Patterns =
    [
        ("Aadhaar", Aadhaar),
        ("PAN", Pan),
        ("Email", Email),
        ("Phone", IndianMobile),
    ];

    /// <summary>
    /// Every match found, in the order the patterns are checked. A single span of text (e.g. a PAN
    /// that also happens to look like part of a longer digit run) can only match one pattern per
    /// pass since matched Aadhaar/mobile spans are digit-only and PAN/Email spans are not.
    /// </summary>
    public static IReadOnlyList<PiiMatch> Find(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var matches = new List<PiiMatch>();
        foreach (var (type, pattern) in Patterns)
        {
            foreach (Match m in pattern.Matches(text))
                matches.Add(new PiiMatch(type, m.Value));
        }
        return matches;
    }

    public static bool ContainsPii(string text) => Find(text).Count > 0;

    /// <summary>Replaces every detected span with a labeled placeholder, e.g. "[REDACTED:AADHAAR]".</summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = text;
        foreach (var (type, pattern) in Patterns)
            result = pattern.Replace(result, $"[REDACTED:{type.ToUpperInvariant()}]");
        return result;
    }
}
