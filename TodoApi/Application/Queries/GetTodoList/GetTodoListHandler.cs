using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Queries.GetTodoList;

public sealed class GetTodoListHandler(IRepositoryQuery<TodoList> repository)
{
    public async Task<Result<GetTodoListResponse>> Handle(GetTodoListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await repository
            .GetByIdAsync(
                query.Id,
                list => new GetTodoListResponse(
                    list.Id,
                    list.Name,
                    list.CreatedAt,
                    list.Items.Select(i => new TodoListItemResponse(i.Id, i.Name, i.IsComplete)).ToList()),
                ct)
            .ConfigureAwait(false);

        return response is null
            ? Result.Failure<GetTodoListResponse>(ErrorResult.NotFound(nameof(TodoList), query.Id.ToString()))
            : Result.Success(response);
    }
}
