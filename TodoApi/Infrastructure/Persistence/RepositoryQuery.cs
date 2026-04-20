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
    Task<T?> GetByIdAsync<T>(Guid id, Expression<Func<TEntity, T>> projection, CancellationToken ct = default);
    Task<TEntity?> GetByAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct = default);
    IQueryable<TEntity> GetQueryable();
    IQueryable<TEntity> GetActiveQueryable();
    Task<PagedResponse<T>> GetPaginatedAsync<T>(
        int page, int pageSize,
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
    Task<IEnumerable<T>> GetAllAsync<T>(Expression<Func<TEntity, T>> projection, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllActiveAsync<T>(Expression<Func<TEntity, T>> projection, CancellationToken ct = default);
    Task<bool> Any(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}

/// <summary>
/// Base class for command (write) repositories.
/// Allows tracking for write operations.
/// </summary>
public abstract class RepositoryCommand<TEntity>(TodoContext context) : IRepositoryCommand<TEntity> where TEntity : Entity
{
    protected DbSet<TEntity> DbSetEntities => context.Set<TEntity>();

    protected TodoContext Context => context;

    public async Task<TEntity?> GetByIdAsync(Guid id, bool tracking = true, CancellationToken ct = default)
    {
        var result = !tracking
            ? await DbSetEntities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false)
            : await DbSetEntities.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

        return result;
    }

    public async Task<TEntity?> GetByAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool tracking = true,
        CancellationToken ct = default)
    {
        var e = DbSetEntities.Where(predicate);
        if (!tracking)
        {
            e = e.AsNoTracking();
        }

        return await e.FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<TEntity>> GetManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool tracking = true,
        CancellationToken ct = default)
    {
        var query = DbSetEntities.Where(predicate);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public IQueryable<TEntity> GetQueryable(bool tracking = true, CancellationToken ct = default)
    {
        var q = DbSetEntities.AsQueryable();
        if (!tracking)
        {
            q = q.AsNoTracking();
        }

        return q;
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await DbSetEntities.AsNoTracking().AnyAsync(predicate, ct).ConfigureAwait(false);
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        var e = await DbSetEntities.AddAsync(entity, ct).ConfigureAwait(false);
        return e.Entity;
    }

    public TEntity Remove(TEntity entity)
    {
        var r = DbSetEntities.Remove(entity);
        return r.Entity;
    }

    public TEntity Update(TEntity entity)
    {
        var e = DbSetEntities.Update(entity);
        return e.Entity;
    }

    public async Task<int> UpdateSaveChangesAsync(TEntity entity, CancellationToken ct = default)
    {
        Update(entity);

        return await Context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
