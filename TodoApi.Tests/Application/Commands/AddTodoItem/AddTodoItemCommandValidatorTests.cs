using FluentValidation.TestHelper;
using TodoApi.Application.Commands.AddTodoItem;

namespace TodoApi.Tests.Application.Commands.AddTodoItem;

public class AddTodoItemCommandValidatorTests
{
    private readonly AddTodoItemCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenTodoListIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.Empty, "Milk");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.TodoListId)
            .WithErrorCode("todoitems.listid.empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validator_WhenNameIsBlank_ShouldHaveErrorWithEmptyCode(string name)
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.NewGuid(), name);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("todoitems.name.empty");
    }

    [Fact]
    public void Validator_WhenNameExceedsMaximumLength_ShouldHaveErrorWithTooLongCode()
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.NewGuid(), new string('a', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("todoitems.name.too_long");
    }

    [Fact]
    public void Validator_WhenNameIsAtMaximumLength_ShouldNotHaveError()
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.NewGuid(), new string('a', 200));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validator_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.NewGuid(), "Milk");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TodoListId);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
