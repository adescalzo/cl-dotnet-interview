using FluentValidation;

namespace TodoApi.Controllers.TodoItems.Commands;

public sealed class UpdateTodoItemRequestValidator : AbstractValidator<UpdateTodoItemRequest>
{
    public UpdateTodoItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todoitems.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todoitems.name.too_long");
    }
}
