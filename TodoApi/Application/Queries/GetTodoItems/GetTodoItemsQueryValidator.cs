using FluentValidation;

namespace TodoApi.Application.Queries.GetTodoItems;

public sealed class GetTodoItemsQueryValidator : AbstractValidator<GetTodoItemsQuery>
{
    public GetTodoItemsQueryValidator()
    {
        RuleFor(x => x.TodoListId).NotEmpty().WithErrorCode("todoitems.listid.empty");
    }
}
