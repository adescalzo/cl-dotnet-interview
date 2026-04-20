# TodoItems

Endpoints under `/api/todolists/{listId}/items`. Conventions
(ProblemDetails format, status-code map, camelCase JSON, UUID-v7 list ids,
int64 item ids) are described once in
[README.md](./README.md#conventions) and not repeated per endpoint.

> **Heads up — route drift.** The external contract expects these
> endpoints under `/todos`, not `/items`. See
> [drift #1](./README.md#1-todoitem-resource-segment--items-vs-todos).
> This file documents what the controller **currently** implements.

## Endpoints

- [`GET    /api/todolists/{listId}/items`](#get-apitodoliststlistiditems) — list items for a TodoList
- [`POST   /api/todolists/{listId}/items`](#post-apitodoliststlistiditems) — add an item
- [`PUT    /api/todolists/{listId}/items/{itemId}`](#put-apitodoliststlistiditemsitemid) — rename an item
- [`PUT    /api/todolists/{listId}/items/{itemId}/complete`](#put-apitodoliststlistiditemsitemidcomplete) — mark an item complete
- [`DELETE /api/todolists/{listId}/items/{itemId}`](#delete-apitodoliststlistiditemsitemid) — remove an item

All endpoints share these path parameters:

| Param    | Type       | Notes |
|----------|------------|-------|
| `listId` | Guid (v7)  | Owning TodoList id. |
| `itemId` | int64      | TodoItem id. |

---

## `GET /api/todolists/{listId}/items`

List every item belonging to a TodoList.

### Request

No body. No query parameters.

### Happy-path response — `200 OK`

Response body is a JSON **array** (the handler returns `r.Items`, unwrapped
by the `Result → HTTP` helper):

```json
[
  {
    "id": 42,
    "name": "Milk",
    "isComplete": false
  },
  {
    "id": 43,
    "name": "Bread",
    "isComplete": true
  }
]
```

| Field        | Type    | Notes |
|--------------|---------|-------|
| `id`         | int64   | |
| `name`       | string  | |
| `isComplete` | boolean | |

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `listId` is empty-GUID. |
| `NotFound`         | 404    | No TodoList with that `listId`. |

Not-found example:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Resource 'TodoList' with identifier '018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a' was not found.",
  "instance": "/api/todolists/018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a/items",
  "code": "NotFound",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01"
}
```

---

## `POST /api/todolists/{listId}/items`

Add a new item to an existing TodoList.

### Request

`Content-Type: application/json`

```json
{
  "name": "Milk"
}
```

| Field  | Type   | Required | Validation |
|--------|--------|----------|------------|
| `name` | string | **yes**  | non-empty; max 200 chars. |

### Happy-path response — `201 Created`

- `Location` header: URI to `GET /api/todolists/{listId}/items` (the list,
  not the individual item — the controller uses the `GetTodoItems` named
  route with only `listId`).
- Body:

```json
{
  "todoListId": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Milk",
  "isComplete": false
}
```

| Field        | Type       | Notes |
|--------------|------------|-------|
| `todoListId` | Guid (v7)  | |
| `name`       | string     | |
| `isComplete` | boolean    | Always `false` on creation. |

> **Drift flag.** The body **does not contain the new item's `id`** —
> the frontend has no way to address the item it just created without
> re-fetching. See [drift #4](./README.md#4-todoitem-create-response--no-id-field).
> Status code is also `201`, contract expects `200`
> ([drift #2](./README.md#2-create-endpoints-return-201-created-contract-expects-200-ok)).

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `listId` empty-GUID, `name` empty, or `name` > 200 chars. |
| `NotFound`         | 404    | No TodoList with that `listId`. |

Validation example — `name` missing:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Resource 'AddTodoItemCommand' has 1 validation error(s).",
  "instance": "/api/todolists/018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a/items",
  "code": "Validation",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01",
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

Not-found example — same shape as in `GET /api/todolists/{listId}/items`.

---

## `PUT /api/todolists/{listId}/items/{itemId}`

Rename an existing item.

### Request

`Content-Type: application/json`

```json
{
  "name": "Whole Milk"
}
```

| Field  | Type   | Required | Validation |
|--------|--------|----------|------------|
| `name` | string | **yes**  | non-empty; max 200 chars. |

> **Drift flag.** The external contract expects
> `{ "description": string, "completed": boolean }` here. This controller
> only accepts `name` and has no way to toggle completion through this
> endpoint (use `.../complete` below instead).
> See [drift #3](./README.md#3-todoitem-request-body--field-names).

### Happy-path response — `200 OK`

```json
{
  "id": 42,
  "todoListId": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Whole Milk",
  "isComplete": false
}
```

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `listId` empty-GUID, `itemId` ≤ 0, `name` empty, or `name` > 200 chars. |
| `NotFound`         | 404    | No TodoList with that `listId`, **or** list exists but has no item with that `itemId`. |

Two separate not-found causes share the 404 status and differ only in
`detail` and in the `Resource` portion of the message:

- list missing → `"Resource 'TodoList' with identifier '…' was not found."`
- item missing inside an existing list → `"Resource 'TodoItem' with identifier '42' was not found."`

Validation example — `itemId = 0` in the URL:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Resource 'UpdateTodoItemCommand' has 1 validation error(s).",
  "instance": "/api/todolists/018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a/items/0",
  "code": "Validation",
  "traceId": "00-7c8b2f7e4b1e4f6aaf89c3b1e8b0dcae-1b3e2e4c4f5e7a1d-01",
  "errors": {
    "ItemId": ["'Item Id' must be greater than '0'."]
  }
}
```

---

## `PUT /api/todolists/{listId}/items/{itemId}/complete`

Mark an item as complete. Idempotent (repeated calls keep `isComplete`
true).

> **Drift flag.** This endpoint has no counterpart in the external
> contract — completion is expected to happen via the regular
> `PUT /.../{itemId}` with `{ "completed": true }` in the body.
> See [drift #6](./README.md#6-put-complete-endpoint-is-extra).

### Request

No body.

### Happy-path response — `200 OK`

```json
{
  "id": 42,
  "todoListId": "018f3d2e-6a1b-7c4e-9d1a-5b2a4e7c9d3a",
  "name": "Whole Milk",
  "isComplete": true
}
```

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `listId` empty-GUID, or `itemId` ≤ 0. |
| `NotFound`         | 404    | List missing, or item missing inside the list. |

Not-found example — same shape as in
`PUT /api/todolists/{listId}/items/{itemId}`.

---

## `DELETE /api/todolists/{listId}/items/{itemId}`

Remove an item from a TodoList.

### Request

No body.

### Happy-path response — `204 No Content`

No body.

### Errors

| `Error.Definition` | Status | When |
|--------------------|--------|------|
| `Validation`       | 400    | `listId` empty-GUID, or `itemId` ≤ 0. |
| `NotFound`         | 404    | List missing, or item missing inside the list. |

Not-found example — same shape as in
`GET /api/todolists/{listId}/items`.
