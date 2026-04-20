using FluentValidation;

namespace TodoApi.Application.Commands.RemoveTodoItem;

public sealed class RemoveTodoItemCommandValidator : AbstractValidator<RemoveTodoItemCommand>
{
    public RemoveTodoItemCommandValidator()
    {
        RuleFor(x => x.TodoListId).NotEmpty().WithErrorCode("todoitems.listid.empty");

        RuleFor(x => x.ItemId).GreaterThan(0).WithErrorCode("todoitems.id.invalid");
    }
}
