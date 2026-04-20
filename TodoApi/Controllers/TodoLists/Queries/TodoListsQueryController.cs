using Microsoft.AspNetCore.Mvc;
using TodoApi.Application.Queries.GetTodoList;
using TodoApi.Application.Queries.GetTodoLists;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using Wolverine;

namespace TodoApi.Controllers.TodoLists.Queries;

[Route("api/todolists")]
[ApiController]
public class TodoListsQueryController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetTodoLists(CancellationToken ct)
    {
        var query = new GetTodoListsQuery();
        var result = await bus.InvokeAsync<Result<GetTodoListsResponse>>(query, ct).ConfigureAwait(false);

        return result.ToOk(r => r.TodoLists);
    }

    [HttpGet("{id:guid}", Name = "GetTodoList")]
    public async Task<IResult> GetTodoList(Guid id, CancellationToken ct)
    {
        var query = new GetTodoListQuery(id);
        var result = await bus.InvokeAsync<Result<GetTodoListResponse>>(query, ct).ConfigureAwait(false);

        return result.ToOk();
    }
}
