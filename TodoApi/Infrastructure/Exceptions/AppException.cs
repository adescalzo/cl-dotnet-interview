namespace TodoApi.Infrastructure.Exceptions;

public class AppException : Exception
{
    public AppException() { }

    public AppException(string description)
        : base(description) { }

    public AppException(string description, Exception ex)
        : base(description, ex) { }
}
