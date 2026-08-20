namespace R2WAI.Application.Features.Members;

public static class ReferralCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        return new string(chars);
    }
}
