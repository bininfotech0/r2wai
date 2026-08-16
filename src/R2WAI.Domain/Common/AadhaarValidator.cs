namespace R2WAI.Domain.Common;

/// <summary>
/// Structural validation only — confirms a 12-digit number is checksum-valid per the public
/// Verhoeff algorithm (the same check digit scheme Aadhaar numbers use). This does NOT verify
/// the number against UIDAI's registry; no such integration exists. A number passing this check
/// is "structurally valid," never "verified."
/// </summary>
public static class AadhaarValidator
{
    private static readonly int[,] Multiplication =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 },
        { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
        { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 },
        { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 },
        { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
        { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] Permutation =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 },
        { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
        { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 },
        { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 },
        { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };

    public static bool IsStructurallyValid(string? aadhaarNumber)
    {
        if (string.IsNullOrWhiteSpace(aadhaarNumber) || aadhaarNumber.Length != 12)
            return false;

        if (!aadhaarNumber.All(char.IsDigit))
            return false;

        // Aadhaar numbers never start with 0 or 1.
        if (aadhaarNumber[0] is '0' or '1')
            return false;

        var checksum = 0;
        var digits = aadhaarNumber.Reverse().Select(c => c - '0').ToArray();
        for (var i = 0; i < digits.Length; i++)
            checksum = Multiplication[checksum, Permutation[i % 8, digits[i]]];

        return checksum == 0;
    }
}
