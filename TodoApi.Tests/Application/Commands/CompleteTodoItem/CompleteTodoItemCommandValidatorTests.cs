using FluentValidation.TestHelper;
using TodoApi.Application.Commands.CompleteTodoItem;

namespace TodoApi.Tests.Application.Commands.CompleteTodoItem;

public class CompleteTodoItemCommandValidatorTests
{
    private readonly CompleteTodoItemCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenTodoListIdIsEmpty_ShouldHaveErrorWithEmptyCode()
    {
        // Arrange
        var command = new CompleteTodoItemCommand(Guid.Empty, 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result
            .ShouldHaveValidationErrorFor(x => x.TodoListId)
            .WithErrorCode("todoitems.listid.empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validator_WhenItemIdIsNotPositive_ShouldHaveErrorWithInvalidCode(long itemId)
    {
        // Arrange
        var command = new CompleteTodoItemCommand(Guid.NewGuid(), itemId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ItemId).WithErrorCode("todoitems.id.invalid");
    }

    [Fact]
    public void Validator_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new CompleteTodoItemCommand(Guid.NewGuid(), 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TodoListId);
        result.ShouldNotHaveValidationErrorFor(x => x.ItemId);
    }
}
