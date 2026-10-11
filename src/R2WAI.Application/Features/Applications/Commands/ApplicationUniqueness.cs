namespace R2WAI.Application.Features.Applications.Commands;

/// <summary>
/// Tenant-scoped uniqueness checks for connected systems, shared by create, update and rollback so
/// a rename through any path is held to the same rule. Name is compared case-insensitively after
/// trimming: "HR Portal" and " hr portal" are the same system to a user picking one from a list.
/// The tenant filter comes from the global query filter, so other tenants are never visible here.
/// </summary>
internal static class ApplicationUniqueness
{
    public static async Task EnsureNameIsFreeAsync(
        IRepository<ConnectedApplication> applicationRepo, string name, Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();
        var clash = await applicationRepo.FirstOrDefaultAsync(
            a => a.Name.ToLower() == normalized && (excludeId == null || a.Id != excludeId),
            cancellationToken);

        if (clash is not null)
            throw new ValidationException("Name",
                $"A connected system named '{clash.Name}' already exists in this workspace.");
    }
}
