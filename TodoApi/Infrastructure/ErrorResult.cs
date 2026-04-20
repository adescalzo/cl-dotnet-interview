namespace TodoApi.Infrastructure;

public sealed record ErrorResult(
    string Code,
    ErrorDefinition Definition,
    string Description,
    Dictionary<string, object>? Metadata = null
)
{
    public static readonly ErrorResult None = new(
        nameof(ErrorDefinition.None),
        ErrorDefinition.None,
        string.Empty
    );

    public static ErrorResult Error(string description) =>
        new(nameof(ErrorDefinition.Error), ErrorDefinition.Error, description);

    public static ErrorResult Error(string code, string description) =>
        new(code, ErrorDefinition.Error, description);

    public static ErrorResult NotFound(string resource, string id) =>
        new(
            nameof(ErrorDefinition.NotFound),
            ErrorDefinition.NotFound,
            $"Resource '{resource}' with identifier '{id}' was not found.",
            new Dictionary<string, object> { ["Resource"] = resource, ["Id"] = id }
        );

    public static ErrorResult Validation(string resource, Dictionary<string, string[]> errors) =>
        new(
            nameof(ErrorDefinition.Validation),
            ErrorDefinition.Validation,
            $"Resource '{resource}' has {errors.Count} validation error(s).",
            new Dictionary<string, object> { ["Resource"] = resource, ["Errors"] = errors }
        );

    public static ErrorResult Validation(string resource, Dictionary<string, string> errors)
    {
        var errorsArray = errors.ToDictionary(kvp => kvp.Key, kvp => new[] { kvp.Value });

        return Validation(resource, errorsArray);
    }

    public static ErrorResult Conflict(string resource, string message) =>
        new(
            nameof(ErrorDefinition.Conflict),
            ErrorDefinition.Conflict,
            message,
            new Dictionary<string, object> { ["Resource"] = resource }
        );

    public static ErrorResult Unauthorized(string description) =>
        new(nameof(ErrorDefinition.Unauthorized), ErrorDefinition.Unauthorized, description);
}
