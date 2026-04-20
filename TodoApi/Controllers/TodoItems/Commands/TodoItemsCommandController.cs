using Microsoft.AspNetCore.Mvc;
using TodoApi.Application.Commands.AddTodoItem;
using TodoApi.Application.Commands.CompleteTodoItem;
using TodoApi.Application.Commands.RemoveTodoItem;
using TodoApi.Application.Commands.UpdateTodoItem;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using Wolverine;

namespace TodoApi.Controllers.TodoItems.Commands;

[Route("api/todolists/{listId:guid}/items")]
[ApiController]
public class TodoItemsCommandController(IMessageBus bus) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> PostTodoItem(
        Guid listId,
        AddTodoItemRequest payload,
        CancellationToken ct
    )
    {
        var command = new AddTodoItemCommand(listId, payload.Name);
        var result = await bus.InvokeAsync<Result<AddTodoItemResponse>>(command, ct)
            .ConfigureAwait(false);

        return result.ToCreated(_ => Url.Link("GetTodoItems", new { listId }) ?? string.Empty);
    }

    [HttpPut("{itemId:long}")]
    public async Task<IResult> PutTodoItem(
        Guid listId,
        long itemId,
        UpdateTodoItemRequest payload,
        CancellationToken ct
    )
    {
        var command = new UpdateTodoItemCommand(listId, itemId, payload.Name);
        var result = await bus.InvokeAsync<Result<UpdateTodoItemResponse>>(command, ct)
            .ConfigureAwait(false);

        return result.ToOk();
    }

    [HttpPut("{itemId:long}/complete")]
    public async Task<IResult> CompleteTodoItem(Guid listId, long itemId, CancellationToken ct)
    {
        var command = new CompleteTodoItemCommand(listId, itemId);
        var result = await bus.InvokeAsync<Result<CompleteTodoItemResponse>>(command, ct)
            .ConfigureAwait(false);

        return result.ToOk();
    }

    [HttpDelete("{itemId:long}")]
    public async Task<IResult> DeleteTodoItem(Guid listId, long itemId, CancellationToken ct)
    {
        var command = new RemoveTodoItemCommand(listId, itemId);
        var result = await bus.InvokeAsync<Result>(command, ct).ConfigureAwait(false);

        return result.ToNoContent();
    }
}
