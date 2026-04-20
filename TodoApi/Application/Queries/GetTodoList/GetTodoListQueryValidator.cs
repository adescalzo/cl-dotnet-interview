using FluentValidation;

namespace TodoApi.Application.Queries.GetTodoList;

public sealed class GetTodoListQueryValidator : AbstractValidator<GetTodoListQuery>
{
    public GetTodoListQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithErrorCode("todolists.id.empty");
    }
}
