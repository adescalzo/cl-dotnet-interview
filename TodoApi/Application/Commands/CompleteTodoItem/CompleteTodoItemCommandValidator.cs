using FluentValidation;

namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed class CompleteTodoItemCommandValidator : AbstractValidator<CompleteTodoItemCommand>
{
    public CompleteTodoItemCommandValidator()
    {
        RuleFor(x => x.TodoListId).NotEmpty().WithErrorCode("todoitems.listid.empty");

        RuleFor(x => x.ItemId).GreaterThan(0).WithErrorCode("todoitems.id.invalid");
    }
}
