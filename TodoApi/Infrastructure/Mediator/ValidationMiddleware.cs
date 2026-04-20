using FluentValidation;
using FluentValidation.Results;
using TodoApi.Infrastructure;

namespace TodoApi.Infrastructure.Mediator;

/// <summary>
/// Wolverine middleware that runs FluentValidation validators before the handler executes.
/// On failure the chain is short-circuited with a Result.Failure carrying
/// ErrorResult.Validation — no exceptions are thrown for expected validation failures.
/// See ADR-0006 and ADR-0007.
/// </summary>
public static class ValidationMiddleware
{
    public static async ValueTask<Result<T>?> BeforeAsync<T, TMessage>(
        TMessage message,
        IEnumerable<IValidator<TMessage>> validators,
        CancellationToken cancellationToken)
        where TMessage : notnull
    {
        var failures = await CollectFailuresAsync(message, validators, cancellationToken).ConfigureAwait(false);
        return failures is null ? null : Result.Failure<T>(BuildError<TMessage>(failures));
    }

    public static async ValueTask<Result?> BeforeAsync<TMessage>(
        TMessage message,
        IEnumerable<IValidator<TMessage>> validators,
        CancellationToken cancellationToken)
        where TMessage : notnull
    {
        var failures = await CollectFailuresAsync(message, validators, cancellationToken).ConfigureAwait(false);
        return failures is null ? null : Result.Failure(BuildError<TMessage>(failures));
    }

    private static async Task<List<ValidationFailure>?> CollectFailuresAsync<TMessage>(
        TMessage message,
        IEnumerable<IValidator<TMessage>> validators,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validators);

        List<ValidationFailure>? failures = null;
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken).ConfigureAwait(false);
            if (result.IsValid)
            {
                continue;
            }

            failures ??= [];
            failures.AddRange(result.Errors);
        }

        return failures;
    }

    private static ErrorResult BuildError<TMessage>(List<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.Ordinal);

        var resource = typeof(TMessage).Name;
        return ErrorResult.Validation(resource, errors);
    }
}
