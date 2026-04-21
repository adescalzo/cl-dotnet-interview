using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Tests.TestSupport;

internal sealed class TodoListCommandRepository(TodoContext context)
    : RepositoryCommand<TodoList>(context),
        ITodoListRepositoryCommand;
