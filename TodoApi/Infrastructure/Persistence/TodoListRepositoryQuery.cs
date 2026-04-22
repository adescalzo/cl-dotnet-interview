using TodoApi.Data;
using TodoApi.Data.Entities;

namespace TodoApi.Infrastructure.Persistence;

public interface ITodoListRepositoryQuery : IRepositoryQuery<TodoList>;

public sealed class TodoListRepositoryQuery(TodoContext context)
    : RepositoryQuery<TodoList>(context),
        ITodoListRepositoryQuery;
