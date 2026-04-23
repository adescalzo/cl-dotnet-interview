using Refit;
using TodoApi.Application.ExternalApi.Payloads;

namespace TodoApi.Application.ExternalApi;

public interface IExternalTodoApiClient
{
    [Get("/todolists")]
    Task<IReadOnlyList<ExternalTodoList>> GetAllAsync(CancellationToken ct = default);

    [Post("/todolists")]
    Task<ExternalTodoList> CreateTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        [Body] CreateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}")]
    Task<ExternalTodoList> UpdateTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        [Body] UpdateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}")]
    Task DeleteTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task<ExternalTodoItem> UpdateTodoItemAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        string todoitemId,
        [Body] UpdateExternalTodoItemRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task DeleteTodoItemAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        string todoitemId,
        CancellationToken ct = default
    );
}
