using FluentValidation;

namespace TodoApi.Controllers.TodoItems.Commands;

public sealed class AddTodoItemRequestValidator : AbstractValidator<AddTodoItemRequest>
{
    public AddTodoItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todoitems.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todoitems.name.too_long");
    }
}
