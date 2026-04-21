using FluentValidation;

namespace TodoApi.Controllers.TodoLists.Commands;

public sealed class UpdateTodoListRequestValidator : AbstractValidator<UpdateTodoListRequest>
{
    public UpdateTodoListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("todolists.name.empty")
            .MaximumLength(200)
            .WithErrorCode("todolists.name.too_long");
    }
}
