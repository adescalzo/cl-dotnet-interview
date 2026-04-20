namespace TodoApi.Infrastructure.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string description) : base(description)
    {
    }

    public NotFoundException(string description, Exception ex) : base(description, ex)
    {
    }

    public NotFoundException(string resource, string id)
        : base($"Resource '{resource}' with identifier '{id}' was not found.")
    {
        Resource = resource;
        Id = id;
    }

    public string Resource { get; } = string.Empty;
    public string Id { get; } = Guid.Empty.ToString();
}
