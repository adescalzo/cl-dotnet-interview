using Microsoft.AspNetCore.Mvc;
using TodoApi.Application.Commands.CreateTodoList;
using TodoApi.Application.Commands.DeleteTodoList;
using TodoApi.Application.Commands.UpdateTodoList;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using Wolverine;

namespace TodoApi.Controllers.TodoLists.Commands;

[Route("api/todolists")]
[ApiController]
public class TodoListsCommandController(IMessageBus bus) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> PostTodoList(CreateTodoListRequest payload, CancellationToken ct)
    {
        var command = new CreateTodoListCommand(payload.Name);
        var result = await bus.InvokeAsync<Result<CreateTodoListResponse>>(command, ct).ConfigureAwait(false);

        return result.ToCreated(value => Url.Link("GetTodoList", new { id = value.Id }) ?? string.Empty);
    }

    [HttpPut("{id:guid}")]
    public async Task<IResult> PutTodoList(Guid id, UpdateTodoListRequest payload, CancellationToken ct)
    {
        var command = new UpdateTodoListCommand(id, payload.Name);
        var result = await bus.InvokeAsync<Result<UpdateTodoListResponse>>(command, ct).ConfigureAwait(false);

        return result.ToOk();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteTodoList(Guid id, CancellationToken ct)
    {
        var command = new DeleteTodoListCommand(id);
        var result = await bus.InvokeAsync<Result>(command, ct).ConfigureAwait(false);

        return result.ToNoContent();
    }
}
