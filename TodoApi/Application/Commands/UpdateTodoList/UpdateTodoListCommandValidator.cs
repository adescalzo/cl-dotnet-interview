using FluentValidation;

namespace TodoApi.Application.Commands.UpdateTodoList;

public sealed class UpdateTodoListCommandValidator : AbstractValidator<UpdateTodoListCommand>
{
    public UpdateTodoListCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode("todolists.id.empty");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todolists.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todolists.name.too_long");
    }
}
