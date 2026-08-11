using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// The central domain entity: an existing (government) application that R2WAI connects to.
/// Every assistant, API, knowledge base, tool, workflow and policy is configured per application.
/// Class name is <c>ConnectedApplication</c> because the simple name <c>Application</c> is shadowed by the
/// <c>R2WAI.Application</c> namespace everywhere in the <c>R2WAI.*</c> tree (see ADR-0001).
/// </summary>
public sealed class ConnectedApplication : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public string? BaseUrl { get; private set; }
    public ApplicationEnvironment Environment { get; private set; } = ApplicationEnvironment.Development;
    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Draft;
    public Guid? ManagerUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public Department Department { get; private set; } = null!;

    private ConnectedApplication() { }

    public ConnectedApplication(Guid id, Guid tenantId, Guid departmentId, string name, string code,
                                string? description = null, string? baseUrl = null)
    {
        Id = id;
        TenantId = tenantId;
        DepartmentId = departmentId;
        Name = name;
        Code = code;
        Description = description;
        BaseUrl = baseUrl;
        Environment = ApplicationEnvironment.Development;
        Status = ApplicationStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, string? baseUrl,
                              ApplicationEnvironment environment)
    {
        EnsureMutable();
        Name = name;
        Description = description;
        BaseUrl = baseUrl;
        Environment = environment;
        MarkAsModified();
    }

    public void SetManager(Guid? managerUserId)
    {
        EnsureMutable();
        ManagerUserId = managerUserId;
        MarkAsModified();
    }

    public void StartDiscovery()
    {
        EnsureMutable();
        Status = ApplicationStatus.Discovering;
        MarkAsModified();
    }

    public void MarkConfiguring()
    {
        EnsureMutable();
        Status = ApplicationStatus.Configuring;
        MarkAsModified();
    }

    public void MarkTesting()
    {
        EnsureMutable();
        Status = ApplicationStatus.Testing;
        MarkAsModified();
    }

    public void Publish()
    {
        EnsureMutable();
        Status = ApplicationStatus.Published;
        PublishedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Disable()
    {
        EnsureMutable();
        Status = ApplicationStatus.Disabled;
        MarkAsModified();
    }

    public void Enable()
    {
        EnsureMutable();
        Status = ApplicationStatus.Published;
        MarkAsModified();
    }

    public void Archive()
    {
        EnsureMutable();
        Status = ApplicationStatus.Archived;
        MarkAsModified();
    }

    private void EnsureMutable()
    {
        if (Status == ApplicationStatus.Archived)
            throw new InvalidOperationException("Archived applications cannot be modified.");
    }
}
