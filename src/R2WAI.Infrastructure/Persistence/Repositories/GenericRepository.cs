using System.Linq.Expressions;

namespace R2WAI.Infrastructure.Persistence.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity<Guid>
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<T>> GetAllAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbSet.CountAsync(cancellationToken);
        var items = await _dbSet
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public virtual async Task<PagedResult<T>> GetAllAsync(Expression<Func<T, bool>> predicate, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(predicate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Delete(T entity)
    {
        if (entity is BaseEntity<Guid> baseEntity)
        {
            baseEntity.SoftDelete();
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual IQueryable<T> Include(Expression<Func<T, object>> includeExpression)
    {
        return _dbSet.Include(includeExpression);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }
}
