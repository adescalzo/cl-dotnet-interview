using FluentValidation.TestHelper;
using TodoApi.Application.Queries.GetTodoList;

namespace TodoApi.Tests.Application.Queries.GetTodoList;

public class GetTodoListQueryValidatorTests
{
    private readonly GetTodoListQueryValidator _validator = new();

    [Fact]
    public void Validator_WhenIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var query = new GetTodoListQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id).WithErrorCode("todolists.id.empty");
    }

    [Fact]
    public void Validator_WhenIdIsNotEmpty_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetTodoListQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }
}
