using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Tests.TestSupport;

/// <summary>
/// Test-only implementation of <see cref="IRepositoryQuery{TodoList}"/>.
/// Only the methods exercised by the query handlers are backed; the rest throw
/// so that accidental use in a test is loud.
/// </summary>
internal sealed class TodoListQueryRepository(TodoContext context) : IRepositoryQuery<TodoList>
{
    public async Task<TodoList?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.TodoList.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

    public async Task<T?> GetByIdAsync<T>(
        Guid id,
        Expression<Func<TodoList, T>> projection,
        CancellationToken ct = default
    ) =>
        await context
            .TodoList.Where(x => x.Id == id)
            .Select(projection)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    public Task<IEnumerable<TodoList>> GetAllAsync(CancellationToken ct = default) =>
        throw new NotSupportedException();

    public async Task<IEnumerable<T>> GetAllAsync<T>(
        Expression<Func<TodoList, T>> projection,
        CancellationToken ct = default
    ) => await context.TodoList.Select(projection).ToListAsync(ct).ConfigureAwait(false);

    public Task<TodoList?> GetByAsync(
        Expression<Func<TodoList, bool>> filter,
        CancellationToken ct = default
    ) => throw new NotSupportedException();

    public IQueryable<TodoList> GetQueryable() => throw new NotSupportedException();

    public IQueryable<TodoList> GetActiveQueryable() => throw new NotSupportedException();

    public Task<PagedResponse<T>> GetPaginatedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<TodoList, T>> projection,
        Expression<Func<TodoList, bool>>? filter,
        CancellationToken ct = default
    ) => throw new NotSupportedException();

    public Task<IEnumerable<T>> GetAsync<T>(
        Expression<Func<TodoList, T>> projection,
        Expression<Func<TodoList, bool>>? filter,
        CancellationToken ct = default
    ) => throw new NotSupportedException();

    public Task<IEnumerable<T>> GetAllActiveAsync<T>(
        Expression<Func<TodoList, T>> projection,
        CancellationToken ct = default
    ) => throw new NotSupportedException();

    public Task<bool> Any(
        Expression<Func<TodoList, bool>> predicate,
        CancellationToken ct = default
    ) => throw new NotSupportedException();
}
