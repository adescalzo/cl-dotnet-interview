namespace TodoApi.Infrastructure.Exceptions;

public class ValidationException : Exception
{
    public ValidationException()
    {
    }

    public ValidationException(string description) : base(description)
    {
    }

    public ValidationException(string description, Exception ex) : base(description, ex)
    {
    }

    public ValidationException(string description, Dictionary<string, string> properties) : base(description)
    {
        Properties = properties;
    }

    public Dictionary<string, string> Properties { get; } = [];
}
