# 0013 - Refit for typed external HTTP clients

- Status: accepted
- Date: 2026-04-21
- Deciders: TodoApi team

## Context and problem statement

The sync module needs to call the external Todo API (six endpoints, fully
specified in `docs/external-api.yaml`). We need a typed client that maps
C# method calls to HTTP requests without hand-rolling `HttpClient`
boilerplate for each endpoint.

The client must integrate with `IHttpClientFactory` (for lifetime
management, Polly policy attachment, and testability), serialize
request bodies in `snake_case` to match the external API contract, and
handle non-success responses without leaking `HttpClient` internals into
application code.

## Decision drivers

- Zero hand-rolled HTTP boilerplate — the contract is defined once as a
  C# interface; Refit generates the implementation.
- First-class `IHttpClientFactory` integration via
  `Refit.HttpClientFactory` — named client lifetime managed by the
  framework, Polly policies attachable via `.AddPolicyHandler()`.
- `CancellationToken` support on every method — required for
  `IJobExecutionContext.CancellationToken` propagation from Quartz jobs.
- Testable: `IExternalTodoApiClient` is an interface; NSubstitute can
  stub it in unit tests without spinning up HTTP.
- The OpenAPI spec is already written — Refit is a direct translation
  of that spec into C# with no ceremony.

## Considered options

- **Refit** — interface-based; code-gen at build time; `IHttpClientFactory`
  integration; well-maintained; no runtime reflection beyond initial
  setup.
- **RestSharp** — fluent builder API; less interface-first; no native
  `IHttpClientFactory` integration.
- **Hand-rolled `HttpClient`** — full control; no dependency; high
  boilerplate; error-prone for serialization and query-string encoding.
- **NSwag / Kiota generated client** — full code-gen from OpenAPI; heavy
  output for six endpoints; overkill.

## Decision outcome

Chosen option: **Refit (`Refit` + `Refit.HttpClientFactory`)**.

---

### Conventions

#### 1. Interface location and naming

The interface and its DTOs live in `Application/ExternalApi/`:

```
Application/
  ExternalApi/
    IExternalTodoApiClient.cs   ← Refit interface
    Dtos/
      ExternalTodoList.cs
      ExternalTodoItem.cs
      CreateExternalTodoListRequest.cs
      CreateExternalTodoItemRequest.cs
      UpdateExternalTodoListRequest.cs
      UpdateExternalTodoItemRequest.cs
```

`Application` owns the interface (it is a port). `Infrastructure` owns
the registration (wiring to `IHttpClientFactory`). No Refit types leak
into `Domain`.

#### 2. Interface definition

```csharp
public interface IExternalTodoApiClient
{
    [Get("/todolists")]
    Task<IReadOnlyList<ExternalTodoList>> GetAllAsync(
        CancellationToken ct = default);

    [Post("/todolists")]
    Task<ExternalTodoList> CreateTodoListAsync(
        [Body] CreateExternalTodoListRequest body,
        CancellationToken ct = default);

    [Patch("/todolists/{todolistId}")]
    Task<ExternalTodoList> UpdateTodoListAsync(
        string todolistId,
        [Body] UpdateExternalTodoListRequest body,
        CancellationToken ct = default);

    [Delete("/todolists/{todolistId}")]
    Task DeleteTodoListAsync(string todolistId, CancellationToken ct = default);

    [Patch("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task<ExternalTodoItem> UpdateTodoItemAsync(
        string todolistId,
        string todoitemId,
        [Body] UpdateExternalTodoItemRequest body,
        CancellationToken ct = default);

    [Delete("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task DeleteTodoItemAsync(
        string todolistId,
        string todoitemId,
        CancellationToken ct = default);
}
```

#### 3. Serialization

The external API uses `snake_case` field names. Configure via
`RefitSettings` with `SystemTextJsonContentSerializer`:

```csharp
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
};
```

DTOs use C# `PascalCase` property names. The serializer handles the
translation — no `[JsonPropertyName]` attributes needed.

#### 4. Registration

`Infrastructure/Configuration/ExternalApiExtensions.cs`:

```csharp
public static IServiceCollection AddExternalApiClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services
        .AddRefitClient<IExternalTodoApiClient>(refitSettings)
        .ConfigureHttpClient(c =>
        {
            c.BaseAddress = new Uri(configuration["ExternalApi:BaseUrl"]!);
            c.Timeout = TimeSpan.FromSeconds(20);  // outer; Polly controls per-attempt
        })
        .AddPolicyHandler(ResiliencePolicies.RetryPolicy())   // ADR-0014
        .AddPolicyHandler(ResiliencePolicies.CircuitBreakerPolicy());
    return services;
}
```

Called from `InfrastructureExtensions.AddInfrastructure(...)`.

#### 5. 404 on DELETE — idempotent success

DELETE on the external API returns 204 on success and 404 if the record
is already gone. Both are correct outcomes. Catch `ApiException` with
`HttpStatusCode.NotFound` on DELETE calls and treat it as success:

```csharp
try
{
    await _client.DeleteTodoListAsync(externalId, ct);
}
catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // already deleted — idempotent success
}
```

Do not silence all `ApiException`s — only the DELETE + 404 case.

#### 6. Error handling on non-DELETE calls

Refit throws `ApiException` on any non-success response. Let Polly
(ADR-0014) handle retryable errors. For non-retryable 4xx, let the
exception propagate to the per-entity catch in the job, which marks the
event `Failed` and logs the status + response body.

#### 7. Configuration

```json
// appsettings.json
"ExternalApi": {
  "BaseUrl": "http://localhost:3000",
  "MaxRetries": 3
}
```

Override `BaseUrl` via environment variable `ExternalApi__BaseUrl`.
Never hardcode the URL in C# code.

#### 8. Testing

Inject `IExternalTodoApiClient` as a constructor parameter. In unit
tests, substitute with NSubstitute:

```csharp
var client = Substitute.For<IExternalTodoApiClient>();
client.GetAllAsync(Arg.Any<CancellationToken>())
      .Returns(new List<ExternalTodoList> { ... });
```

No `HttpClient` or network required in unit tests.

### Consequences

- Positive: six endpoints, zero HTTP boilerplate. The interface is the
  spec — it is readable and reviewable.
- Positive: `IHttpClientFactory` manages socket lifetime; no
  `HttpClient` disposal issues.
- Positive: Polly policies attach at the `HttpClient` level without any
  change to the interface or application code (ADR-0014).
- Positive: NSubstitute can stub the interface directly — jobs are fully
  testable without HTTP.
- Negative: `ApiException` from Refit wraps the HTTP response; callers
  must know to unwrap `StatusCode` and `Content` from it rather than
  catching `HttpRequestException` directly. Document in team standards.
- Negative: Refit generates the implementation at runtime via
  `Castle.Core` proxies. Negligible overhead in practice; not a
  concern for a low-frequency sync job.
- Neutral: DTOs are records in `Application`. If the external API
  contract changes, the DTOs and the interface update together — a
  single change point.

## Links

- Builds on: ADR-0012 (Quartz jobs that consume this client).
- Related: ADR-0014 (Polly policies attached to this client's `HttpClient`).
- Library: <https://github.com/reactiveui/refit>
