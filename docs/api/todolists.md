# TodoLists

Endpoints under `/api/todolists`. Conventions (ProblemDetails format,
status-code map, camelCase JSON, UUID-v7 ids) are described once in
[README.md](./README.md#conventions) and not repeated per endpoint.

## Endpoints

- [`GET    /api/todolists`](#get-apitodolists) — list all TodoLists
- [`POST   /api/todolists`](#post-apitodolists) — create a TodoList
- [`GET    /api/todolists/{id}`](#get-apitodoliststid) — fetch one TodoList with items
- [`PUT    /api/todolists/{id}`](#put-apitodoliststid) — rename a TodoList
- [`DELETE /api/todolists/{id}`](#delete-apitodoliststid) — delete a TodoList

---

## `GET /api/todolists`

List every TodoList, each with its nested items.

### Request

No body. No query parameters.

### Happy-path response — `200 OK`

Response body is a JSON **array** (the handler returns `r.TodoLists`, so
the wrapper object is unwrapped by the `Result → HTTP` helper):

```json
[
  {
    "id": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
    "name": "Groceries",
    "createdAt": "2026-04-19T10:15:00.0000000Z",
    "items": [
      {
        "id": 42,
        "name": "Milk",
        "isComplete": false
      }
    ]
  }
]
```

| Field          | Type            | Notes |
|----------------|-----------------|-------|
| `id`           | Guid (v7)       | TodoList id. |
| `name`         | string          | |
| `createdAt`    | string (ISO-8601 UTC) | |
| `items`        | array of object | May be empty. |
| `items[].id`   | int64           | TodoItem id. |
| `items[].name` | string          | |
| `items[].isComplete` | boolean   | |

### Errors

None under normal operation; only the generic 500 path (see
[README — Unhandled 500](./README.md#unhandled-500)).

---

## `POST /api/todolists`

Create a new TodoList.

### Request

`Content-Type: application/json`

```json
{
  "name": "Groceries"
}
```

| Field  | Type   | Required | Validation |
|--------|--------|----------|------------|
| `name` | string | **yes**  | non-empty; max 200 chars. |

### Happy-path response — `201 Created`

- `Location` header: URI to `GET /api/todolists/{id}` for the new resource.
- Body:

```json
{
  "id": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Groceries",
  "createdAt": "2026-04-20T14:03:11.0000000Z"
}
```

> See [drift #2](./README.md#2-create-endpoints-return-201-created-contract-expects-200-ok) — the external contract expects `200 OK` here.

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `name` empty, or longer than 200 chars. |

Example — `name` missing:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Resource 'CreateTodoListCommand' has 1 validation error(s).",
  "instance": "/api/todolists",
  "code": "Validation",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01",
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

---

## `GET /api/todolists/{id}`

Fetch a single TodoList with its items.

### Path parameters

| Param | Type      | Notes |
|-------|-----------|-------|
| `id`  | Guid (v7) | TodoList id. |

### Request

No body.

### Happy-path response — `200 OK`

```json
{
  "id": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Groceries",
  "createdAt": "2026-04-19T10:15:00.0000000Z",
  "items": [
    {
      "id": 42,
      "name": "Milk",
      "isComplete": false
    }
  ]
}
```

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `NotFound`         | 404    | No TodoList with that `id`. |

Example — id not found:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Resource 'TodoList' with identifier '018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a' was not found.",
  "instance": "/api/todolists/018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "code": "NotFound",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01"
}
```

> Note: a validator exists (`GetTodoListQueryValidator`) that rejects the
> all-zero GUID with a `todolists.id.empty` rule. In practice the
> route constraint `:guid` means any request that reaches the handler has
> a parseable GUID, but an explicit `00000000-0000-0000-0000-000000000000`
> in the URL will produce the 400 validation response with
> `errors.Id = ["'Id' must not be empty."]`.

---

## `PUT /api/todolists/{id}`

Rename an existing TodoList.

### Path parameters

| Param | Type      | Notes |
|-------|-----------|-------|
| `id`  | Guid (v7) | TodoList id. |

### Request

`Content-Type: application/json`

```json
{
  "name": "Shopping"
}
```

| Field  | Type   | Required | Validation |
|--------|--------|----------|------------|
| `name` | string | **yes**  | non-empty; max 200 chars. |

### Happy-path response — `200 OK`

```json
{
  "id": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Shopping",
  "updatedAt": "2026-04-20T14:10:00.0000000Z"
}
```

`updatedAt` is nullable — may be `null` in edge cases if the domain did
not record an update timestamp.

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `id` is empty-GUID, `name` empty, or `name` > 200 chars. |
| `NotFound`         | 404    | No TodoList with that `id`. |

Validation example — name too long:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Resource 'UpdateTodoListCommand' has 1 validation error(s).",
  "instance": "/api/todolists/018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "code": "Validation",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01",
  "errors": {
    "Name": ["The length of 'Name' must be 200 characters or fewer. You entered 340 characters."]
  }
}
```

Not-found example — same shape as in `GET /api/todolists/{id}`.

---

## `DELETE /api/todolists/{id}`

Delete a TodoList (and its items, by cascade in the domain).

### Path parameters

| Param | Type      | Notes |
|-------|-----------|-------|
| `id`  | Guid (v7) | TodoList id. |

### Request

No body.

### Happy-path response — `204 No Content`

No body.

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `id` is empty-GUID. |
| `NotFound`         | 404    | No TodoList with that `id`. |

Not-found example — same shape as in `GET /api/todolists/{id}`.
