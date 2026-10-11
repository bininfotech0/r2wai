namespace R2WAI.Application.Common.AI;

/// <summary>
/// Tenant-wide capability (ToolDefinition) name rules. What has to be unique is the *sanitized*
/// function name the model sees, not the raw text: the runtime tool factories silently drop the
/// second of any two capabilities whose sanitized names match, so "get-user" and "Get User" would
/// otherwise both save fine and one would quietly never be callable. Inactive capabilities count
/// too — they can be activated later, at which point the clash would surface at runtime.
///
/// Admin-entered names (create/update) are rejected on a clash via <see cref="EnsureFreeAsync"/>;
/// machine-generated ones (OpenAPI discovery, MCP import) are made unique via <see cref="MakeUnique"/>.
/// </summary>
public static class CapabilityNames
{
    private const int MaxFunctionNameLength = 64;

    /// <summary>Sanitized names of every capability in the tenant, optionally excluding one.</summary>
    public static async Task<HashSet<string>> LoadTakenAsync(
        IRepository<ToolDefinition> repo, Guid tenantId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var all = await repo.FindAsync(t => t.TenantId == tenantId, cancellationToken);
        return all
            .Where(t => excludeId == null || t.Id != excludeId)
            .Select(t => ToolFunctionName.Sanitize(t.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static async Task EnsureFreeAsync(
        IRepository<ToolDefinition> repo, Guid tenantId, string name, Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var all = await repo.FindAsync(t => t.TenantId == tenantId, cancellationToken);
        var functionName = ToolFunctionName.Sanitize(name);
        var clash = all.FirstOrDefault(t =>
            (excludeId == null || t.Id != excludeId)
            && string.Equals(ToolFunctionName.Sanitize(t.Name), functionName, StringComparison.OrdinalIgnoreCase));

        if (clash is not null)
            throw new ValidationException("Name", string.Equals(clash.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)
                ? $"A capability named '{clash.Name}' already exists in this workspace."
                : $"'{name}' is too similar to the existing capability '{clash.Name}' — assistants would see both as '{functionName}'.");
    }

    /// <summary>
    /// First free of: <paramref name="suggested"/>, "&lt;prefix&gt;_&lt;suggested&gt;" (when a prefix is
    /// given), then "…_2", "…_3".
    /// Adds the chosen name to <paramref name="taken"/> so later calls in the same batch can't reuse it.
    /// </summary>
    public static string MakeUnique(string suggested, string? prefix, HashSet<string> taken)
    {
        var chosen = Pick(suggested, prefix, taken);
        taken.Add(ToolFunctionName.Sanitize(chosen));
        return chosen;
    }

    private static string Pick(string suggested, string? prefix, HashSet<string> taken)
    {
        if (!taken.Contains(ToolFunctionName.Sanitize(suggested)))
            return suggested;

        var prefixed = string.IsNullOrWhiteSpace(prefix) ? suggested : $"{prefix.Trim()}_{suggested}";
        if (!taken.Contains(ToolFunctionName.Sanitize(prefixed)))
            return prefixed;

        for (var i = 2; ; i++)
        {
            // The suffix must survive Sanitize's 64-char cap, so trim the stem rather than the suffix.
            var suffix = $"_{i}";
            var stem = prefixed.Length + suffix.Length > MaxFunctionNameLength
                ? prefixed[..(MaxFunctionNameLength - suffix.Length)]
                : prefixed;
            var candidate = stem + suffix;
            if (!taken.Contains(ToolFunctionName.Sanitize(candidate)))
                return candidate;
        }
    }
}
