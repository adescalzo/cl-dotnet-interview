using FluentValidation;

namespace TodoApi.Application.Commands.DeleteTodoList;

public sealed class DeleteTodoListCommandValidator : AbstractValidator<DeleteTodoListCommand>
{
    public DeleteTodoListCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithErrorCode("todolists.id.empty");
    }
}
