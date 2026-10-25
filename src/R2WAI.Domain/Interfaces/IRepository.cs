using System.Linq.Expressions;
using R2WAI.Domain.Common;

namespace R2WAI.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity<Guid>
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(Guid id, Expression<Func<T, object>> include, CancellationToken cancellationToken = default);
    /// <summary>Loads a nested include chain (e.g. "UserRoles.Role") that a single-property Expression can't express.</summary>
    Task<T?> GetByIdAsync(Guid id, string includePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    async Task<(IReadOnlyList<T> Items, int TotalCount)> FindPagedAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, DateTime>> orderBy,
        int page,
        int pageSize,
        bool descending = false,
        CancellationToken cancellationToken = default)
    {
        // Compatibility fallback for small test/fake repositories. The production EF repository
        // overrides this with SQL-side paging.
        var matching = await FindAsync(predicate, cancellationToken);
        var selector = orderBy.Compile();
        var ordered = descending
            ? matching.OrderByDescending(selector).ThenBy(entity => entity.Id).ToList()
            : matching.OrderBy(selector).ThenBy(entity => entity.Id).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, ordered.Count);
    }
    /// <summary>Loads a nested include chain (e.g. "UserRoles.Role") that a single-property Expression can't express.</summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, string includePath, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
