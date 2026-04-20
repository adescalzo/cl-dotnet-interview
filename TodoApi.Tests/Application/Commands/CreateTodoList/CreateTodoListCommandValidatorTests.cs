using FluentValidation.TestHelper;
using TodoApi.Application.Commands.CreateTodoList;

namespace TodoApi.Tests.Application.Commands.CreateTodoList;

public class CreateTodoListCommandValidatorTests
{
    private readonly CreateTodoListCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validator_WhenNameIsBlank_ShouldHaveErrorWithEmptyCode(string name)
    {
        // Arrange
        var command = new CreateTodoListCommand(name);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("todolists.name.empty");
    }

    [Fact]
    public void Validator_WhenNameExceedsMaximumLength_ShouldHaveErrorWithTooLongCode()
    {
        // Arrange
        var command = new CreateTodoListCommand(new string('a', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("todolists.name.too_long");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Groceries")]
    public void Validator_WhenNameIsValid_ShouldNotHaveError(string name)
    {
        // Arrange
        var command = new CreateTodoListCommand(name);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validator_WhenNameIsAtMaximumLength_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand(new string('a', 200));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
