namespace TodoApi.Infrastructure.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException() { }

    public ForbiddenException(string description)
        : base(description) { }

    public ForbiddenException(string description, Exception ex)
        : base(description, ex) { }
}
