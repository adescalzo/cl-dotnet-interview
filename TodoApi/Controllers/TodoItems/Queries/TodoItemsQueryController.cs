using Microsoft.AspNetCore.Mvc;
using TodoApi.Application.Queries.GetTodoItems;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using Wolverine;

namespace TodoApi.Controllers.TodoItems.Queries;

[Route("api/todolists/{listId:guid}/items")]
[ApiController]
public class TodoItemsQueryController(IMessageBus bus) : ControllerBase
{
    [HttpGet(Name = "GetTodoItems")]
    public async Task<IResult> GetTodoItems(Guid listId, CancellationToken ct)
    {
        var query = new GetTodoItemsQuery(listId);
        var result = await bus.InvokeAsync<Result<GetTodoItemsResponse>>(query, ct)
            .ConfigureAwait(false);

        return result.ToOk(r => r.Items);
    }
}
