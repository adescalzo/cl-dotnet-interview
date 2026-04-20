using System.Linq.Expressions;

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
