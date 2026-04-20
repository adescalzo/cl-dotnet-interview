using FluentValidation.TestHelper;
using TodoApi.Application.Commands.RemoveTodoItem;

namespace TodoApi.Tests.Application.Commands.RemoveTodoItem;

public class RemoveTodoItemCommandValidatorTests
{
    private readonly RemoveTodoItemCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenTodoListIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var command = new RemoveTodoItemCommand(Guid.Empty, 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.TodoListId)
            .WithErrorCode("todoitems.listid.empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validator_WhenItemIdIsNotPositive_ShouldHaveErrorWithInvalidCode(long itemId)
    {
        // Arrange
        var command = new RemoveTodoItemCommand(Guid.NewGuid(), itemId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ItemId).WithErrorCode("todoitems.id.invalid");
    }

    [Fact]
    public void Validator_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new RemoveTodoItemCommand(Guid.NewGuid(), 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TodoListId);
        result.ShouldNotHaveValidationErrorFor(x => x.ItemId);
    }
}
