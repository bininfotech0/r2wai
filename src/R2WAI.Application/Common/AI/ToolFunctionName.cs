using System.Text.RegularExpressions;

namespace R2WAI.Application.Common.AI;

/// <summary>
/// The function name a capability is exposed to the model under. Kernel function names must be
/// letters/digits/underscore only; ToolDefinition.Name is free text, so this maps arbitrary names
/// into that space deterministically rather than rejecting anything with spaces or punctuation.
/// Lives here (not in Infrastructure) so anything creating capabilities can check, up front, that a
/// new name won't collide at runtime — the tool factories silently drop the second of two tools
/// whose sanitized names match.
/// </summary>
public static partial class ToolFunctionName
{
    [GeneratedRegex(@"[^A-Za-z0-9_]")]
    private static partial Regex InvalidNameChars();

    public static string Sanitize(string name)
    {
        var sanitized = InvalidNameChars().Replace(name.Trim(), "_");
        if (sanitized.Length == 0 || char.IsDigit(sanitized[0]))
            sanitized = "tool_" + sanitized;
        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }
}
