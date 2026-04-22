using System.Net;
using System.Security.Cryptography;
using Polly;
using Polly.Extensions.Http;
using Serilog;

namespace TodoApi.Infrastructure.Configuration;

public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> RetryPolicy(int maxRetries = 3) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: (attempt, _, _) =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(0, 1000)),
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    Log.Warning(
                        "Retry {Attempt}/{Max} for {Method} {Uri} — {Status} — waiting {DelayMs}ms",
                        attempt,
                        maxRetries,
                        outcome.Result?.RequestMessage?.Method,
                        outcome.Result?.RequestMessage?.RequestUri,
                        outcome.Result?.StatusCode ?? (object?)outcome.Exception?.GetType().Name,
                        (int)delay.TotalMilliseconds
                    );
                    return Task.CompletedTask;
                }
            );

    public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (_, duration) =>
                    Log.Warning("Circuit breaker OPEN for {Duration}s", duration.TotalSeconds),
                onReset: () => Log.Information("Circuit breaker CLOSED"),
                onHalfOpen: () => Log.Information("Circuit breaker HALF-OPEN — probing")
            );
}
