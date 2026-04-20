using Wolverine;

namespace TodoApi.Infrastructure.Mediator;

/// <summary>
/// Wolverine middleware that logs command and query execution.
/// Applied to all handlers via policy configuration.
/// Implements cross-cutting concern logging pattern.
/// Uses Envelope to access message information without resolving 'object' type.
/// </summary>
public sealed class LoggingMiddleware
{
    public void Before(Envelope envelope, ILogger<LoggingMiddleware> logger) =>
        logger.ExecutingMessage(envelope.MessageType ?? "Unknown");

    public void After(Envelope envelope, ILogger<LoggingMiddleware> logger) =>
        logger.CompletedMessage(envelope.MessageType ?? "Unknown");

    public void OnException(Exception ex, Envelope envelope, ILogger<LoggingMiddleware> logger) =>
        logger.FailedMessage(ex, envelope.MessageType ?? "Unknown", ex.Message);
}

internal static partial class LoggingMiddlewareLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Executing {MessageType}")]
    public static partial void ExecutingMessage(this ILogger logger, string messageType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Completed {MessageType}")]
    public static partial void CompletedMessage(this ILogger logger, string messageType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed {MessageType}: {ErrorMessage}"
    )]
    public static partial void FailedMessage(
        this ILogger logger,
        Exception exception,
        string messageType,
        string errorMessage
    );
}
