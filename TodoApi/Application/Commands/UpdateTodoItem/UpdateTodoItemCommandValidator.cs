using FluentValidation;

namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed class UpdateTodoItemCommandValidator : AbstractValidator<UpdateTodoItemCommand>
{
    public UpdateTodoItemCommandValidator()
    {
        RuleFor(x => x.TodoListId).NotEmpty().WithErrorCode("todoitems.listid.empty");

        RuleFor(x => x.ItemId).GreaterThan(0).WithErrorCode("todoitems.id.invalid");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todoitems.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todoitems.name.too_long");
    }
}
