using System.Text.RegularExpressions;

namespace R2WAI.Application.Common.Security;

/// <summary>
/// Lightweight regex-based detector for common Indian government/citizen PII patterns
/// (Aadhaar, PAN, mobile number, email). Pattern matching only — no checksum validation
/// (e.g. Aadhaar's Verhoeff digit) — so treat results as "worth a human look", not proof.
/// Used to flag content before it's indexed into a knowledge base or sent to an external AI
/// provider, not to block it outright.
/// </summary>
public static class PiiScanner
{
    private static readonly Regex AadhaarPattern = new(@"\b\d{4}\s?\d{4}\s?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex PanPattern = new(@"\b[A-Z]{5}\d{4}[A-Z]\b", RegexOptions.Compiled);
    private static readonly Regex MobilePattern = new(@"\b(?:\+91[\-\s]?)?[6-9]\d{9}\b", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    public static IReadOnlyList<string> Scan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var found = new List<string>(4);
        if (AadhaarPattern.IsMatch(text)) found.Add("Aadhaar");
        if (PanPattern.IsMatch(text)) found.Add("PAN");
        if (MobilePattern.IsMatch(text)) found.Add("MobileNumber");
        if (EmailPattern.IsMatch(text)) found.Add("Email");
        return found;
    }
}
