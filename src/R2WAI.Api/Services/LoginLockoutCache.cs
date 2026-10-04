namespace R2WAI.Api.Services;

// Extracted from AuthController so AdminController's unlock action (docs/api/MISSING-BACKEND-
// ENDPOINTS.md §3.4 #70) can clear exactly the same cache entry the login flow writes, rather than
// duplicating the key format and risking the two ever drifting apart.
public sealed class LoginAttemptState
{
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public static class LoginLockoutCache
{
    public static string Key(string normalizedEmail) => $"login-lockout:{normalizedEmail}";
}
