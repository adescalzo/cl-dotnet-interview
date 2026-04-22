namespace TodoApi.Controllers.TodoItems.Commands;

public class AddTodoItemRequest
{
    public required string Name { get; set; }

    public required int Order { get; set; }
}
