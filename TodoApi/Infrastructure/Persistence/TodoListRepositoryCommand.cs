using TodoApi.Data;
using TodoApi.Data.Entities;

namespace TodoApi.Infrastructure.Persistence;

public interface ITodoListRepositoryCommand : IRepositoryCommand<TodoList>;

public sealed class TodoListRepositoryCommand(TodoContext context)
    : RepositoryCommand<TodoList>(context),
        ITodoListRepositoryCommand;
