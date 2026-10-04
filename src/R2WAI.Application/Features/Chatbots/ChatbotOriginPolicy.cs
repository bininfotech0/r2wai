using System.Text.Json;

namespace R2WAI.Application.Features.Chatbots;

/// <summary>
/// Pure parse/match logic behind Chatbot.AllowedOrigins (JSON-encoded string[], same storage
/// convention as SuggestedQuestions). Null/empty content means "not configured" — every origin is
/// allowed, matching the widget's pre-existing default behavior so tenants who never touch this
/// field see no change. A tenant who does configure it opts into a real allowlist.
/// </summary>
public static class ChatbotOriginPolicy
{
    /// <summary>
    /// Returns null when no restriction is configured (empty content, or content that fails to
    /// parse — malformed data here can only come from a bug, not a caller, so this stays additive
    /// like the rest of the Policy Engine rather than a new way to accidentally lock a chatbot out).
    /// Returns a possibly-empty array when a restriction IS configured.
    /// </summary>
    public static string[]? TryParseAllowedOrigins(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<string[]>(content) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// True if the request should be allowed: no restriction configured, or the browser's Origin
    /// header exactly matches one of the configured origins (case-insensitive scheme+host+port
    /// comparison — a bare origin has no path/trailing slash to normalize). A missing Origin header
    /// (non-browser callers) passes only when no restriction is configured — once a tenant opts
    /// into an allowlist, only requests that present a matching Origin get through.
    /// </summary>
    public static bool IsOriginAllowed(string[]? allowedOrigins, string? requestOrigin)
    {
        if (allowedOrigins is null)
            return true;

        if (string.IsNullOrWhiteSpace(requestOrigin))
            return false;

        return allowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), requestOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }
}
