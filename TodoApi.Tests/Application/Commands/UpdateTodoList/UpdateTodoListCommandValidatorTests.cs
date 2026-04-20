using FluentValidation.TestHelper;
using TodoApi.Application.Commands.UpdateTodoList;

namespace TodoApi.Tests.Application.Commands.UpdateTodoList;

public class UpdateTodoListCommandValidatorTests
{
    private readonly UpdateTodoListCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var command = new UpdateTodoListCommand(Guid.Empty, "Name");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorCode("todolists.id.empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validator_WhenNameIsBlank_ShouldHaveErrorWithEmptyCode(string name)
    {
        // Arrange
        var command = new UpdateTodoListCommand(Guid.NewGuid(), name);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorCode("todolists.name.empty");
    }

    [Fact]
    public void Validator_WhenNameExceedsMaximumLength_ShouldHaveErrorWithTooLongCode()
    {
        // Arrange
        var command = new UpdateTodoListCommand(Guid.NewGuid(), new string('a', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorCode("todolists.name.too_long");
    }

    [Fact]
    public void Validator_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new UpdateTodoListCommand(Guid.NewGuid(), "Groceries");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
