using FluentValidation;

namespace TodoApi.Application.Commands.AddTodoItem;

public sealed class AddTodoItemCommandValidator : AbstractValidator<AddTodoItemCommand>
{
    public AddTodoItemCommandValidator()
    {
        RuleFor(x => x.TodoListId).NotEmpty().WithErrorCode("todoitems.listid.empty");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todoitems.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todoitems.name.too_long");
    }
}
