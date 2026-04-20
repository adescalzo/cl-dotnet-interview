using FluentValidation;
using TodoApi.Application.Commands.CreateTodoList;
using TodoApi.Application.Commands.DeleteTodoList;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Mediator;

namespace TodoApi.Tests.Infrastructure.Mediator;

public sealed class ValidationMiddlewareTests
{
    [Fact]
    public async Task BeforeAsync_WhenCommandIsInvalid_ReturnsValidationFailure()
    {
        var validators = new IValidator<CreateTodoListCommand>[]
        {
            new CreateTodoListCommandValidator(),
        };

        var result = await ValidationMiddleware.BeforeAsync<
            CreateTodoListResponse,
            CreateTodoListCommand
        >(new CreateTodoListCommand(string.Empty), validators, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(ErrorDefinition.Validation, result.Error.Definition);

        var errors = Assert.IsType<Dictionary<string, string[]>>(result.Error.Metadata!["Errors"]);
        Assert.True(errors.ContainsKey(nameof(CreateTodoListCommand.Name)));
    }

    [Fact]
    public async Task BeforeAsync_WhenCommandIsValid_ReturnsNullToContinue()
    {
        var validators = new IValidator<CreateTodoListCommand>[]
        {
            new CreateTodoListCommandValidator(),
        };

        var result = await ValidationMiddleware.BeforeAsync<
            CreateTodoListResponse,
            CreateTodoListCommand
        >(new CreateTodoListCommand("Valid name"), validators, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BeforeAsync_VoidCommand_WhenInvalid_ReturnsNonGenericValidationFailure()
    {
        var validators = new IValidator<DeleteTodoListCommand>[]
        {
            new DeleteTodoListCommandValidator(),
        };

        var result = await ValidationMiddleware.BeforeAsync<DeleteTodoListCommand>(
            new DeleteTodoListCommand(Guid.Empty),
            validators,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(ErrorDefinition.Validation, result.Error.Definition);
    }
}
