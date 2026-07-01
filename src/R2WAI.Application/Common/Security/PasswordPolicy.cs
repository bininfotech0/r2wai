namespace R2WAI.Application.Common.Security;

public static class PasswordPolicy
{
    public const int MinLength = 10;

    public static bool IsValid(string password, out string errorMessage)
    {
        if (password.Length < MinLength)
        {
            errorMessage = $"Password must be at least {MinLength} characters.";
            return false;
        }
        if (!password.Any(char.IsUpper))
        {
            errorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }
        if (!password.Any(char.IsLower))
        {
            errorMessage = "Password must contain at least one lowercase letter.";
            return false;
        }
        if (!password.Any(char.IsDigit))
        {
            errorMessage = "Password must contain at least one digit.";
            return false;
        }
        if (password.All(char.IsLetterOrDigit))
        {
            errorMessage = "Password must contain at least one special character.";
            return false;
        }
        errorMessage = string.Empty;
        return true;
    }
}
