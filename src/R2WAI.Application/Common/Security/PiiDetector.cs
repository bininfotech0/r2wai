using System.Text.RegularExpressions;

namespace R2WAI.Application.Common.Security;

public record PiiMatch(string Type, string Value);

/// <summary>
/// The one detector for Indian citizen PII (Aadhaar, PAN, mobile number, email). Used to flag
/// documents at ingestion (DocumentService), and to block or redact text before it reaches a model
/// (PiiPolicyService). There used to be a second copy (PiiScanner) whose patterns had drifted:
/// ingestion missed hyphenated Aadhaar and lowercase PAN that the policy check caught.
///
/// Pattern matching only, no checksum validation (Aadhaar's Verhoeff digit is not checked), so it
/// leans towards over-matching: for redaction, a false positive costs a few characters and a false
/// negative leaks an identifier.
/// </summary>
public static partial class PiiDetector
{
    // Order matters for Redact: Aadhaar runs before Phone, so "919876543210" (12 digits) is redacted
    // as Aadhaar rather than leaving "91" + a redacted phone.
    private static readonly (string Type, Regex Pattern)[] Patterns =
    [
        ("Aadhaar", Aadhaar()),
        ("PAN", Pan()),
        ("Email", Email()),
        ("Phone", IndianMobile()),
    ];

    public static IReadOnlyList<PiiMatch> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var normalized = Normalize(text);
        var matches = new List<PiiMatch>();
        foreach (var (type, pattern) in Patterns)
        {
            foreach (Match m in pattern.Matches(normalized))
                matches.Add(new PiiMatch(type, m.Value));
        }
        return matches;
    }

    /// <summary>Distinct PII type names found, e.g. ["Aadhaar", "Phone"]; for logging and audit.</summary>
    public static IReadOnlyList<string> FindTypes(string? text) =>
        Find(text).Select(m => m.Type).Distinct().ToList();

    public static bool ContainsPii(string? text) => Find(text).Count > 0;

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = Normalize(text);
        foreach (var (type, pattern) in Patterns)
            result = pattern.Replace(result, $"[REDACTED:{type.ToUpperInvariant()}]");
        return result;
    }

    /// <summary>
    /// Drops invisible format characters (zero-width space/joiner, BOM, …). Inserting them between
    /// digits defeats every pattern while the number still reads normally to a person.
    /// </summary>
    private static string Normalize(string text) => FormatChars().Replace(text, string.Empty);

    // 12 digits, optionally grouped 4-4-4 by a space, hyphen or dot. Digit lookarounds instead of \b
    // so a run of 13+ digits is not matched, while "ID123456789012" still is.
    [GeneratedRegex(@"(?<!\d)\d{4}[\s.\-]?\d{4}[\s.\-]?\d{4}(?!\d)")]
    private static partial Regex Aadhaar();

    // 5 letters, 4 digits, 1 letter; either case, optionally spaced as "ABCDE 1234 F".
    [GeneratedRegex(@"\b[A-Za-z]{5}\s?\d{4}\s?[A-Za-z]\b")]
    private static partial Regex Pan();

    // Indian mobile: optional +91 / 0 prefix, then 10 digits starting 6-9, optionally grouped 5-5.
    [GeneratedRegex(@"(?<![\d+])(?:(?:\+91|0)[\s\-]?)?[6-9]\d{4}[\s\-]?\d{5}(?!\d)")]
    private static partial Regex IndianMobile();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Email();

    [GeneratedRegex(@"\p{Cf}")]
    private static partial Regex FormatChars();
}
