using Refit;
using TodoApi.Application.ExternalApi.Dtos;

namespace TodoApi.Application.ExternalApi;

public interface IExternalTodoApiClient
{
    [Get("/todolists")]
    Task<IReadOnlyList<ExternalTodoList>> GetAllAsync(CancellationToken ct = default);

    [Post("/todolists")]
    Task<ExternalTodoList> CreateTodoListAsync(
        [Body] CreateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}")]
    Task<ExternalTodoList> UpdateTodoListAsync(
        string todolistId,
        [Body] UpdateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}")]
    Task DeleteTodoListAsync(string todolistId, CancellationToken ct = default);

    [Post("/todolists/{todolistId}/todoitems")]
    Task<ExternalTodoItem> CreateTodoItemAsync(
        string todolistId,
        [Body] CreateExternalTodoItemRequest body,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task<ExternalTodoItem> UpdateTodoItemAsync(
        string todolistId,
        string todoitemId,
        [Body] UpdateExternalTodoItemRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task DeleteTodoItemAsync(string todolistId, string todoitemId, CancellationToken ct = default);
}
