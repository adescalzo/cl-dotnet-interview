using Microsoft.AspNetCore.Http.HttpResults;

namespace TodoApi.Infrastructure;

public enum ErrorDefinition
{
    None,
    Error,
    NotFound,
    Validation,
    Conflict,
    Unauthorized
}

public class Result
{
    protected Result(ErrorResult[] errors)
    {
        IsSuccess = errors is { Length: 0 };
        IsFailure = !IsSuccess;
        Error = IsSuccess ? ErrorResult.None : errors[0];
        Errors = errors ?? [];
    }

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public ErrorResult Error { get; }
    public IEnumerable<ErrorResult> Errors { get; }

    public string GetErrorMessage() => string.Join(", ", Errors.Select(x => $"{x.Definition}|{x.Description}"));

    public static Result Success() => new([]);

    public static Result<T> Success<T>(T value) => Result<T>.CreateSuccess(value);

    public static Result Failure(ErrorResult error) => new([error]);

    public static Result Failure(ErrorResult[] errors) => new(errors);

    public static Result<T> Failure<T>(ErrorResult error, T? value = default)
        => Result<T>.CreateFailure([error], value);

    public static Result<T> Failure<T>(ErrorResult[] errors, T? value = default)
        => Result<T>.CreateFailure(errors, value);
}

public class Result<T> : Result
{
    private Result(ErrorResult[] errors, T? value) : base(errors)
    {
        Value = value;
    }

    public T? Value { get; }
    public T GetValue => Value ?? throw new InvalidOperationException("Value is null");
    public bool HasValue => Value is not null;

    internal static Result<T> CreateSuccess(T value) => new([], value);

    internal static Result<T> CreateFailure(ErrorResult[] errors, T? value) => new(errors, value);
}
