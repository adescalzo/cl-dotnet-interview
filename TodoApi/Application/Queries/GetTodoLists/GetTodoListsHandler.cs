using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Queries.GetTodoLists;

public sealed class GetTodoListsHandler(
    ITodoListRepositoryQuery repository,
    ILogger<GetTodoListsHandler> logger
)
{
    public async Task<Result<GetTodoListsResponse>> Handle(
        GetTodoListsQuery query,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var summaries = (
            await repository
                .GetAllAsync(
                    list => new TodoListSummary(
                        list.Id,
                        list.Name,
                        list.CreatedAt,
                        list.Items.Select(i => new TodoItemSummary(i.Id, i.Name, i.IsComplete))
                            .ToList()
                    ),
                    ct
                )
                .ConfigureAwait(false)
        ).ToList();

        logger.LogTodoListsFetched(summaries.Count);

        return Result.Success(new GetTodoListsResponse(summaries));
    }
}

internal static partial class GetTodoListsHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        EventName = "TodoListsFetched",
        Message = "Fetched {Count} TodoLists"
    )]
    public static partial void LogTodoListsFetched(this ILogger logger, int count);
}
