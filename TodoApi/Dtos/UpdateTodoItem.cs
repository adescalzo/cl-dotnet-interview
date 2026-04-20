namespace TodoApi.Dtos;

public class UpdateTodoItem
{
    public required string Name { get; set; }
    public required bool IsComplete { get; set; }
}
