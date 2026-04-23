# REQ-007: Refit External API Client

**Epic:** EPIC-001  
**Type:** Functional — Critical

---

## Problem Statement

Both sync jobs need to call the external Todo API. The external API contract is fully defined in `docs/external-api.yaml`. A typed, generated client reduces boilerplate and makes the contract explicit in code.

---

## Requirement

Use **Refit** (`Refit.HttpClientFactory`) to define a typed interface for the external API. Register it via `AddRefitClient<T>()` with the Polly retry policy attached.

---

## Specification

### 1. Response DTOs

Defined in `Application/ExternalApi/Dtos/` (snake_case deserialization via `JsonSerializerOptions`):

```csharp
public record ExternalTodoList(
    string Id,
    string? SourceId,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ExternalTodoItem> Items);

public record ExternalTodoItem(
    string Id,
    string? SourceId,
    string Description,
    bool Completed,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

### 2. Request DTOs

```csharp
public record CreateExternalTodoListRequest(
    string SourceId,
    string Name,
    IReadOnlyList<CreateExternalTodoItemRequest> Items);

public record CreateExternalTodoItemRequest(
    string SourceId,
    string Description,
    bool Completed);

public record UpdateExternalTodoListRequest(string Name);

public record UpdateExternalTodoItemRequest(string Description, bool Completed);
```

### 3. Refit interface

Defined in `Application/ExternalApi/IExternalTodoApiClient.cs`:

```csharp
public interface IExternalTodoApiClient
{
    [Get("/todolists")]
    Task<IReadOnlyList<ExternalTodoList>> GetAllAsync(CancellationToken ct = default);

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

### 4. Registration

In `Infrastructure/Configuration/ExternalApiExtensions.cs`:

```csharp
services
    .AddRefitClient<IExternalTodoApiClient>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
    })
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri(configuration["ExternalApi:BaseUrl"]!);
        c.Timeout = TimeSpan.FromSeconds(20);
    })
    .AddPolicyHandler(ResiliencePolicies.RetryPolicy(logger));  // REQ-006
```

### 5. 404 on DELETE

Refit throws `ApiException` on non-success responses. In the jobs, catch `ApiException` with `StatusCode == HttpStatusCode.NotFound` on DELETE calls and treat it as success. Do not propagate.

### 6. Configuration

`appsettings.json`:
```json
"ExternalApi": {
  "BaseUrl": "http://localhost:3000",
  "MaxRetries": 3
}
```

Override `BaseUrl` via environment variable `ExternalApi__BaseUrl` for deployed environments.

---

## Acceptance Criteria

- [ ] `IExternalTodoApiClient` resolves from DI.
- [ ] `GetAllAsync()` returns a typed list of `ExternalTodoList` with nested items.
- [ ] `CreateTodoListAsync()` sends `source_id`, `name`, and `items[]` in snake_case JSON body.
- [ ] `UpdateTodoListAsync()` sends only `name`.
- [ ] `UpdateTodoItemAsync()` sends only `description` and `completed`.
- [ ] `DeleteTodoListAsync()` with 404 response does not throw.
- [ ] `DeleteTodoItemAsync()` with 404 response does not throw.
- [ ] Polly retry policy is applied to this client's `HttpClient`.
- [ ] Base URL loaded from configuration, not hardcoded.
