using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace TodoApi.Infrastructure.Extensions;

public static class ResultExtensions
{
    private static int GetStatusCode(ErrorDefinition definition) =>
        definition switch
        {
            ErrorDefinition.NotFound => StatusCodes.Status404NotFound,
            ErrorDefinition.Validation => StatusCodes.Status400BadRequest,
            ErrorDefinition.Conflict => StatusCodes.Status409Conflict,
            ErrorDefinition.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

    private static string GetProblemType(ErrorDefinition definition) =>
        definition switch
        {
            ErrorDefinition.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            ErrorDefinition.Validation => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ErrorDefinition.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            ErrorDefinition.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        };

    private static string GetTitle(ErrorDefinition definition) =>
        definition switch
        {
            ErrorDefinition.NotFound => "Resource Not Found",
            ErrorDefinition.Validation => "Validation Error",
            ErrorDefinition.Conflict => "Conflict",
            ErrorDefinition.Unauthorized => "Unauthorized",
            ErrorDefinition.Error => "Internal Server Error",
            _ => "An error occurred",
        };

    public static IResult Match<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess,
        Func<Result, IResult>? onFailure = null
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess
            ? onSuccess(result.GetValue)
            : (onFailure ?? (r => r.ToProblemDetails()))(result);
    }

    public static IResult ToOk<T>(this Result<T> result, Func<T, object>? mapper = null)
    {
        return result.Match(onSuccess: value => HttpResults.Ok(mapper?.Invoke(value) ?? value));
    }

    public static IResult ToCreated<T>(
        this Result<T> result,
        string uri,
        Func<T, object>? mapper = null
    )
    {
        return result.Match(onSuccess: value =>
            HttpResults.Created(uri, mapper?.Invoke(value) ?? value)
        );
    }

    public static IResult ToCreated<T>(
        this Result<T> result,
        Func<T, string> uriFactory,
        Func<T, object>? mapper = null
    )
    {
        ArgumentNullException.ThrowIfNull(uriFactory);

        return result.Match(onSuccess: value =>
            HttpResults.Created(uriFactory(value), mapper?.Invoke(value) ?? value)
        );
    }

    public static IResult ToAccepted<T>(
        this Result<T> result,
        string uri,
        Func<T, object>? mapper = null
    )
    {
        return result.Match(onSuccess: value =>
            HttpResults.Accepted(uri, mapper?.Invoke(value) ?? value)
        );
    }

    public static IResult ToNoContent<T>(this Result<T> result)
    {
        return result.Match(onSuccess: _ => HttpResults.NoContent());
    }

    public static IResult ToNoContent(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? HttpResults.NoContent() : result.ToProblemDetails();
    }

    public static IResult ToProblemDetails(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException(
                "Cannot create problem details from a successful result."
            );
        }

        var error = result.Error;
        var statusCode = GetStatusCode(error.Definition);
        var traceId = Activity.Current?.Id;

        if (error.Definition == ErrorDefinition.Validation)
        {
            return HttpResults.Problem(BuildValidationProblem(error, statusCode, traceId));
        }

        var problemDetails = new ProblemDetails
        {
            Type = GetProblemType(error.Definition),
            Title = GetTitle(error.Definition),
            Status = statusCode,
            Detail = error.Description,
        };

        problemDetails.Extensions["code"] = error.Code;
        if (traceId is not null)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        return HttpResults.Problem(problemDetails);
    }

    private static ValidationProblemDetails BuildValidationProblem(
        ErrorResult error,
        int statusCode,
        string? traceId
    )
    {
        var errors =
            error.Metadata is { } metadata
            && metadata.TryGetValue("Errors", out var raw)
            && raw is Dictionary<string, string[]> map
                ? map
                : new Dictionary<string, string[]>(StringComparer.Ordinal);

        var problem = new ValidationProblemDetails(errors)
        {
            Type = GetProblemType(error.Definition),
            Title = GetTitle(error.Definition),
            Status = statusCode,
            Detail = error.Description,
        };

        problem.Extensions["code"] = error.Code;
        if (traceId is not null)
        {
            problem.Extensions["traceId"] = traceId;
        }

        return problem;
    }
}
