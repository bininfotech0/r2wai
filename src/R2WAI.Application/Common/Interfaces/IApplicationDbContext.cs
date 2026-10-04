namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Query-only window onto the persistence layer for application-layer handlers.
/// Implemented by the infrastructure DbContext; keeps controllers free of direct
/// database access (06-BACKEND-STANDARDS.md) without leaking EF types across the boundary.
/// Writes go through IRepository&lt;T&gt; / IUnitOfWork.
/// </summary>
public interface IApplicationDbContext
{
    Guid? TenantId { get; }

    IQueryable<T> Query<T>() where T : class;
}
