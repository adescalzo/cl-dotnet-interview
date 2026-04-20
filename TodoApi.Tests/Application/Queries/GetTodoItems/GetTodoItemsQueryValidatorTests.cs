using FluentValidation.TestHelper;
using TodoApi.Application.Queries.GetTodoItems;

namespace TodoApi.Tests.Application.Queries.GetTodoItems;

public class GetTodoItemsQueryValidatorTests
{
    private readonly GetTodoItemsQueryValidator _validator = new();

    [Fact]
    public void Validator_WhenTodoListIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var query = new GetTodoItemsQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.TodoListId)
            .WithErrorCode("todoitems.listid.empty");
    }

    [Fact]
    public void Validator_WhenTodoListIdIsNotEmpty_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetTodoItemsQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TodoListId);
    }
}
