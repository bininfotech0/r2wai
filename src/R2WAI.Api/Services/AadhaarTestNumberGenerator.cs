namespace R2WAI.Api.Services;

/// <summary>
/// Generates random 12-digit numbers that pass AadhaarValidator.IsStructurallyValid — i.e. the
/// same Verhoeff check-digit scheme Aadhaar numbers use — purely so dev/test seed data doesn't
/// get rejected by that structural check. These are NOT real Aadhaar numbers and are never
/// checked against UIDAI; see the compliance note on AadhaarValidator.
/// </summary>
public static class AadhaarTestNumberGenerator
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

    private static readonly int[] Inverse = [0, 4, 3, 2, 1, 5, 6, 7, 8, 9];

    public static string Generate()
    {
        Span<int> digits = stackalloc int[11];
        digits[0] = Random.Shared.Next(2, 10); // Aadhaar numbers never start with 0 or 1.
        for (var i = 1; i < 11; i++)
            digits[i] = Random.Shared.Next(0, 10);

        var checksum = 0;
        for (var i = 0; i < 11; i++)
        {
            var digit = digits[10 - i]; // process the 11 known digits rightmost-first
            checksum = Multiplication[checksum, Permutation[(i + 1) % 8, digit]];
        }

        var checkDigit = Inverse[checksum];

        Span<char> result = stackalloc char[12];
        for (var i = 0; i < 11; i++)
            result[i] = (char)('0' + digits[i]);
        result[11] = (char)('0' + checkDigit);

        return new string(result);
    }
}
