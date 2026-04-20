using FluentValidation;

namespace TodoApi.Application.Commands.CreateTodoList;

public sealed class CreateTodoListCommandValidator : AbstractValidator<CreateTodoListCommand>
{
    public CreateTodoListCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todolists.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todolists.name.too_long");
    }
}
