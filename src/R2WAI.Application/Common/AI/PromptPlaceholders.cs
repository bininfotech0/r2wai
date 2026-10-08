using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace R2WAI.Application.Common.AI;

/// <summary>
/// Fills the context placeholders an admin can write into a system prompt —
/// <c>{{tenant.name}}</c>, <c>{{workspace.name}}</c>, <c>{{user.name}}</c>, <c>{{user.role}}</c>,
/// <c>{{current_date}}</c>. Before this nothing rendered them, so the model received the literal
/// braces. Unknown placeholders are left untouched rather than guessed at or silently removed.
/// </summary>
public static partial class PromptPlaceholders
{
    public const string TenantName = "tenant.name";
    public const string WorkspaceName = "workspace.name";
    public const string UserName = "user.name";
    public const string UserRole = "user.role";
    public const string CurrentDate = "current_date";

    [GeneratedRegex(@"\{\{\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    public static bool HasPlaceholders(string? template) =>
        !string.IsNullOrEmpty(template) && PlaceholderPattern().IsMatch(template);

    public static string Render(string template, IReadOnlyDictionary<string, string> values) =>
        PlaceholderPattern().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value.ToLowerInvariant(), out var value) ? value : match.Value);
}

public interface IPromptRenderer
{
    /// <summary>
    /// Renders <paramref name="template"/> for a chat in <paramref name="tenantId"/>. The user is the
    /// ambient authenticated caller; an anonymous caller (public widget) renders as a guest.
    /// </summary>
    Task<string> RenderAsync(string template, Guid tenantId, string? workspaceName, CancellationToken ct = default);
}

public sealed class PromptRenderer(IApplicationDbContext db, ICurrentUserService currentUser, TimeProvider clock) : IPromptRenderer
{
    public async Task<string> RenderAsync(string template, Guid tenantId, string? workspaceName, CancellationToken ct = default)
    {
        if (!PromptPlaceholders.HasPlaceholders(template))
            return template;

        var tenantName = await db.Query<Tenant>()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);

        string userName = "Guest";
        if (currentUser.UserId is { } userId && currentUser.TenantId == tenantId)
        {
            var user = await db.Query<User>()
                .Where(u => u.Id == userId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
                userName = $"{user.FirstName} {user.LastName}".Trim();
        }

        var values = new Dictionary<string, string>
        {
            [PromptPlaceholders.TenantName] = tenantName ?? "this organization",
            [PromptPlaceholders.WorkspaceName] = string.IsNullOrWhiteSpace(workspaceName) ? "this workspace" : workspaceName,
            [PromptPlaceholders.UserName] = userName,
            [PromptPlaceholders.UserRole] = currentUser.Roles.Length > 0 ? string.Join(", ", currentUser.Roles) : "Guest",
            [PromptPlaceholders.CurrentDate] = clock.GetUtcNow().ToString("yyyy-MM-dd"),
        };

        return PromptPlaceholders.Render(template, values);
    }
}
