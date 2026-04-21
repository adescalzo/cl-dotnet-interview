using FluentValidation;

namespace TodoApi.Controllers.TodoLists.Commands;

public sealed class CreateTodoListRequestValidator : AbstractValidator<CreateTodoListRequest>
{
    public CreateTodoListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todolists.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todolists.name.too_long");
    }
}
