using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Queries.GetTodoItems;

public sealed class GetTodoItemsHandler(ITodoListRepositoryQuery repository)
{
    public async Task<Result<GetTodoItemsResponse>> Handle(GetTodoItemsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await repository
            .GetByIdAsync(
                query.TodoListId,
                list => new GetTodoItemsResponse(
                    list.Id,
                    list.Items.Select(i => new TodoItemResponse(i.Id, i.Name, i.IsComplete))
                        .ToList()
                ),
                ct
            )
            .ConfigureAwait(false);

        if (response is null)
        {
            return Result.Failure<GetTodoItemsResponse>(
                ErrorResult.NotFound(nameof(TodoList), query.TodoListId.ToString())
            );
        }

        return Result.Success(response);
    }
}
