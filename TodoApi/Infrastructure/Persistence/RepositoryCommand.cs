using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Infrastructure.Persistence;

/// <summary>
/// Base interface for command (write) repository operations.
/// </summary>
public interface IRepositoryCommand<TEntity>
{
    Task<TEntity?> GetByIdAsync(Guid id, bool tracking = true, CancellationToken ct = default);
    Task<TEntity?> GetByAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool tracking = true,
        CancellationToken ct = default
    );
    Task<List<TEntity>> GetManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool tracking = true,
        CancellationToken ct = default
    );
    IQueryable<TEntity> GetQueryable(bool tracking = true, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    TEntity Remove(TEntity entity);
    TEntity Update(TEntity entity);
    Task<int> UpdateSaveChangesAsync(TEntity entity, CancellationToken ct = default);
}


/// <summary>
/// Base class for command (write) repositories.
/// Allows tracking for write operations.
/// </summary>
public class RepositoryCommand<TEntity>(TodoContext context) : IRepositoryCommand<TEntity>
    where TEntity : Entity
{
    protected DbSet<TEntity> DbSetEntities => context.Set<TEntity>();

    protected TodoContext Context => context;

    public async Task<TEntity?> GetByIdAsync(
        Guid id,
        bool tracking = true,
        CancellationToken ct = default
    )
    {
        var result = !tracking
            ? await DbSetEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                .ConfigureAwait(false)
            : await DbSetEntities.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

        return result;
    }

    public async Task<TEntity?> GetByAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool tracking = true,
        CancellationToken ct = default
    )
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
        CancellationToken ct = default
    )
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

    public async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
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
