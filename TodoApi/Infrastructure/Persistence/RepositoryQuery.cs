using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Infrastructure.Persistence;

/// <summary>
/// Base interface for query (read) repository operations.
/// </summary>
public interface IRepositoryQuery<TEntity>
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> GetByIdAsync<T>(
        Guid id,
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    );
    Task<TEntity?> GetByAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default
    );
    IQueryable<TEntity> GetQueryable();
    IQueryable<TEntity> GetActiveQueryable();
    Task<PagedResponse<T>> GetPaginatedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<TEntity, T>> projection,
        Expression<Func<TEntity, bool>>? filter,
        CancellationToken ct = default
    );
    Task<IEnumerable<T>> GetAsync<T>(
        Expression<Func<TEntity, T>> projection,
        Expression<Func<TEntity, bool>>? filter,
        CancellationToken ct = default
    );
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync<T>(
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    );
    Task<IEnumerable<T>> GetAllActiveAsync<T>(
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    );
    Task<bool> Any(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}

/// <summary>
/// Generic implementation of query (read) repository operations.
/// </summary>
public class RepositoryQuery<TEntity>(TodoContext context) : IRepositoryQuery<TEntity>
    where TEntity : Entity
{
    protected DbSet<TEntity> DbSetEntities => context.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await DbSetEntities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

    public virtual async Task<T?> GetByIdAsync<T>(
        Guid id,
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    ) =>
        await DbSetEntities
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(projection)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    public virtual async Task<TEntity?> GetByAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default
    ) =>
        await DbSetEntities.AsNoTracking().FirstOrDefaultAsync(filter, ct).ConfigureAwait(false);

    public virtual IQueryable<TEntity> GetQueryable() => DbSetEntities.AsNoTracking();

    public virtual IQueryable<TEntity> GetActiveQueryable() => DbSetEntities.AsNoTracking();

    public virtual async Task<PagedResponse<T>> GetPaginatedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<TEntity, T>> projection,
        Expression<Func<TEntity, bool>>? filter,
        CancellationToken ct = default
    )
    {
        var query = DbSetEntities.AsNoTracking();
        if (filter != null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResponse<T>(items, totalCount, page, pageSize);
    }

    public virtual async Task<IEnumerable<T>> GetAsync<T>(
        Expression<Func<TEntity, T>> projection,
        Expression<Func<TEntity, bool>>? filter,
        CancellationToken ct = default
    )
    {
        var query = DbSetEntities.AsNoTracking();
        if (filter != null)
        {
            query = query.Where(filter);
        }

        return await query.Select(projection).ToListAsync(ct).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await DbSetEntities.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

    public virtual async Task<IEnumerable<T>> GetAllAsync<T>(
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    ) =>
        await DbSetEntities.AsNoTracking().Select(projection).ToListAsync(ct).ConfigureAwait(false);

    public virtual async Task<IEnumerable<T>> GetAllActiveAsync<T>(
        Expression<Func<TEntity, T>> projection,
        CancellationToken ct = default
    ) =>
        await DbSetEntities.AsNoTracking().Select(projection).ToListAsync(ct).ConfigureAwait(false);

    public virtual async Task<bool> Any(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    ) =>
        await DbSetEntities.AsNoTracking().AnyAsync(predicate, ct).ConfigureAwait(false);
}
