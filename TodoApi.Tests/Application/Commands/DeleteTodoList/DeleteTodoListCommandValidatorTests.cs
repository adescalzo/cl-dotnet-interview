using FluentValidation.TestHelper;
using TodoApi.Application.Commands.DeleteTodoList;

namespace TodoApi.Tests.Application.Commands.DeleteTodoList;

public class DeleteTodoListCommandValidatorTests
{
    private readonly DeleteTodoListCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var command = new DeleteTodoListCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id).WithErrorCode("todolists.id.empty");
    }

    [Fact]
    public void Validator_WhenIdIsNotEmpty_ShouldNotHaveError()
    {
        // Arrange
        var command = new DeleteTodoListCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }
}
