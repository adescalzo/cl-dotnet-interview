namespace TodoApi.Infrastructure.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException()
        : base() { }

    public ConcurrencyException(string description)
        : base(description) { }

    public ConcurrencyException(string description, Exception ex)
        : base(description, ex) { }

    public ConcurrencyException(string resource, string id, int version, int currentVersion)
        : base(
            $"Resource '{resource}' with identifier '{id}' has different version '{version}'. Current version {currentVersion}"
        )
    {
        Resource = resource;
        Id = id;
        Version = version;
        CurrentVersion = currentVersion;
    }

    public string Resource { get; } = string.Empty;
    public string Id { get; } = string.Empty;
    public int Version { get; }
    public int CurrentVersion { get; }
}
