# Frontend API Documentation

This document covers the source-defined authentication, todo, todo category, and todo activity APIs.

## General

Base routes are controller routes under the ASP.NET Core app. JSON responses use camelCase property names. Enums are serialized as strings because `JsonStringEnumConverter` is configured.

Protected endpoints require:

```http
Authorization: Bearer <accessToken>
```

Error responses produced by application exceptions follow this shape:

```json
{
  "success": false,
  "statusCode": 404,
  "errorCode": "TODO_NOT_FOUND",
  "message": "Todo not found",
  "timestamp": "2026-08-24T00:00:00Z",
  "path": "/api/todos/1"
}
```

JWT authentication failures can return `401 Unauthorized` from ASP.NET Core authentication middleware.

## Enums

### TodoStatus

```json
"Todo"
"InProgress"
"Done"
```

### TodoPriority

```json
"Low"
"Medium"
"High"
```

### TodoActivityType

```json
"Created"
"Updated"
"StatusChanged"
"CategoryChanged"
```

## Shared Response Models

### UserResponseDto

```json
{
  "id": 1,
  "username": "alice",
  "email": "alice@example.com",
  "displayName": "Alice",
  "avatarUrl": "https://example.com/avatar.png",
  "role": "User",
  "createdAt": "2026-08-24T00:00:00Z",
  "updatedAt": "2026-08-24T00:00:00Z"
}
```

### TodoDto

`categoryName` is mapped from the loaded `Category` navigation property. `GET /api/todos` includes the category relation; `GET /api/todos/{id}` does not explicitly include it in the current service code.

```json
{
  "id": 1,
  "title": "Write API docs",
  "description": "Document todo APIs",
  "status": "Todo",
  "priority": "Medium",
  "dueDate": "2026-08-25T00:00:00Z",
  "createdAt": "2026-08-24T00:00:00Z",
  "updatedAt": null,
  "isOverdue": false,
  "completedAt": null,
  "isCompletedLate": false,
  "categoryId": 1,
  "categoryName": "Work"
}
```

### TodoCategoryDto

```json
{
  "id": 1,
  "name": "Work"
}
```

### TodoActivityDto

```json
{
  "id": 1,
  "type": "Created",
  "description": "Todo created",
  "createdAt": "2026-08-24T00:00:00Z"
}
```

### Pagination Response Format

`GET /api/todos` returns `PagedResult<TodoDto>`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalItems": 0,
  "totalPages": 0
}
```

Rules:

- `page` less than `1` is treated as `1`.
- `pageSize` less than `1` is treated as `10`.
- `pageSize` greater than `100` is capped at `100`.
- `totalPages` is `ceil(totalItems / pageSize)`.

## Authentication Endpoints

### Register

Method: `POST`

Route: `/api/users/register`

Authorization: none

Path/query parameters: none

Request body:

```json
{
  "username": "alice",
  "email": "alice@example.com",
  "password": "secret",
  "displayName": "Alice",
  "avatarUrl": "https://example.com/avatar.png"
}
```

Response body: empty body with `200 OK`.

Possible error codes:

- `409 Conflict`, `USERNAME_ALREADY_EXISTS`
- `409 Conflict`, `EMAIL_ALREADY_EXISTS`
- `500 Internal Server Error`, `DEFAULT_ROLE_NOT_FOUND`
- `400 Bad Request` for malformed JSON or model binding errors

Business rules:

- `username` must be unique.
- `email` must be unique.
- Password is stored as a BCrypt hash.
- New users receive the default `User` role.
- `createdAt` and `updatedAt` are set to current UTC time.

### Login

Method: `POST`

Route: `/api/users/login`

Authorization: none

Path/query parameters: none

Request body:

```json
{
  "username": "alice",
  "password": "secret"
}
```

Response body:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "user": {
    "id": 1,
    "username": "alice",
    "email": "alice@example.com",
    "displayName": "Alice",
    "avatarUrl": null,
    "role": "User",
    "createdAt": "2026-08-24T00:00:00Z",
    "updatedAt": "2026-08-24T00:00:00Z"
  }
}
```

Possible error codes:

- `401 Unauthorized`, `INVALID_CREDENTIALS`
- `400 Bad Request` for malformed JSON or model binding errors

Business rules:

- Login uses `username` and `password`.
- Invalid username and invalid password return the same error.
- A successful login creates a refresh token that expires after 7 days.
- JWT includes user id, username, email, and role claims.

### Refresh Token

Method: `POST`

Route: `/api/users/refresh-token`

Authorization: none

Path/query parameters: none

Request body:

```json
{
  "refreshToken": "<refresh-token>"
}
```

Response body:

```json
{
  "accessToken": "<new-jwt>",
  "refreshToken": "<same-refresh-token>",
  "user": {
    "id": 1,
    "username": "alice",
    "email": "alice@example.com",
    "displayName": "Alice",
    "avatarUrl": null,
    "role": "User",
    "createdAt": "2026-08-24T00:00:00Z",
    "updatedAt": "2026-08-24T00:00:00Z"
  }
}
```

Possible error codes:

- `401 Unauthorized`, `INVALID_REFRESH_TOKEN`
- `400 Bad Request` for malformed JSON or model binding errors

Business rules:

- Refresh token must exist.
- Refresh token must not be revoked.
- Refresh token `expiresAt` must be greater than or equal to current UTC time.
- The existing refresh token is returned; the service does not rotate it.

## Todo Endpoints

All todo endpoints require an authenticated JWT.

### Get Todos

Method: `GET`

Route: `/api/todos`

Authorization: authenticated user

Path parameters: none

Query parameters:

| Name | Type | Required | Notes |
| --- | --- | --- | --- |
| `search` | string | no | Trims input and matches `title` or `description` using `Contains`. |
| `status` | `TodoStatus` | no | `Todo`, `InProgress`, `Done`. |
| `priority` | `TodoPriority` | no | `Low`, `Medium`, `High`. |
| `page` | integer | no | Default `1`; values below `1` become `1`. |
| `pageSize` | integer | no | Default `10`; values below `1` become `10`; maximum `100`. |
| `sortBy` | string | no | Supported: `title`, `priority`, `status`, `duedate`, `createdat`. Other values default to created date descending. |
| `isDescending` | boolean | no | Default `false`; ignored when `sortBy` is missing or unsupported. |
| `isOverdue` | boolean | no | Filters overdue/non-overdue using current UTC time. |
| `categoryId` | integer | no | Filters todos assigned to a category id. |

Request body: none

Response body: `PagedResult<TodoDto>`

```json
{
  "items": [
    {
      "id": 1,
      "title": "Write API docs",
      "description": "Document todo APIs",
      "status": "Todo",
      "priority": "Medium",
      "dueDate": "2026-08-25T00:00:00Z",
      "createdAt": "2026-08-24T00:00:00Z",
      "updatedAt": null,
      "isOverdue": false,
      "completedAt": null,
      "isCompletedLate": false,
      "categoryId": 1,
      "categoryName": "Work"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim
- `400 Bad Request` for invalid query model binding, such as invalid enum values

Business rules:

- Only todos where `userId` equals the authenticated user id are returned.
- Default sort is `createdAt` descending.
- `categoryName` is populated from the related category when loaded.

### Get Todo

Method: `GET`

Route: `/api/todos/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body: none

Response body: `TodoDto`

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `TODO_NOT_FOUND`
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The todo must belong to the authenticated user.
- A todo owned by another user is treated as not found.
- The service does not explicitly include the category relation for this endpoint, so `categoryName` may be `null` even when `categoryId` has a value.

### Create Todo

Method: `POST`

Route: `/api/todos`

Authorization: authenticated user

Path/query parameters: none

Request body:

```json
{
  "title": "Write API docs",
  "description": "Document todo APIs",
  "priority": "Medium",
  "dueDate": "2026-08-25T00:00:00Z",
  "categoryId": 1
}
```

Response body:

```json
{
  "message": "Todo created successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `CATEGORY_NOT_FOUND`
- `400 Bad Request` for malformed JSON or model binding errors
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- `status` is always initialized to `Todo`.
- `priority` defaults to `Medium` if omitted.
- `userId` is set from the authenticated user.
- `createdAt` is set to current UTC time.
- If `categoryId` is provided, the category must belong to the authenticated user.
- A `Created` activity is added with description `Todo created`.
- Database constraints define `title` as required with max length `200`, and `description` max length `1000`.

### Update Todo

Method: `PUT`

Route: `/api/todos/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body:

```json
{
  "title": "Write updated API docs",
  "description": "Document todo APIs",
  "priority": "High",
  "dueDate": "2026-08-25T00:00:00Z",
  "categoryId": 1
}
```

Response body:

```json
{
  "message": "Todo updated successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `TODO_NOT_FOUND`
- `404 Not Found`, `CATEGORY_NOT_FOUND`
- `400 Bad Request` for malformed JSON or model binding errors
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The todo must belong to the authenticated user.
- A todo owned by another user is treated as not found.
- If `categoryId` is provided, the category must belong to the authenticated user.
- `updatedAt` is set to current UTC time.
- If priority changes, an `Updated` activity is added with description `Priority changed from {oldPriority} to {newPriority}`.
- If category changes, a `CategoryChanged` activity is added with description `Category changed`.
- This endpoint does not update `status` or `completedAt`.
- Database constraints define `title` as required with max length `200`, and `description` max length `1000`.

### Update Todo Status

Method: `PATCH`

Route: `/api/todos/{id}/status`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body:

```json
{
  "status": "Done"
}
```

Response body:

```json
{
  "message": "Todo status updated successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `TODO_NOT_FOUND`
- `409 Conflict`, `INVALID_TODO_STATUS_TRANSITION`
- `400 Bad Request` for malformed JSON or model binding errors
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The todo must belong to the authenticated user.
- A todo owned by another user is treated as not found.
- Same-status updates are allowed.
- Valid status transitions are documented in the status transition section.
- `updatedAt` is set to current UTC time.
- `completedAt` is set to current UTC time only when the new status is `Done`.
- `completedAt` is cleared when the new status is not `Done`.
- A `StatusChanged` activity is added with description `Status changed from {oldStatus} to {newStatus}`.

### Delete Todo

Method: `DELETE`

Route: `/api/todos/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body: none

Response body:

```json
{
  "message": "Todo deleted successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `TODO_NOT_FOUND`
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The todo must belong to the authenticated user.
- A todo owned by another user is treated as not found.
- Todo activities are configured to cascade delete with the todo.

## TodoActivity Endpoints

Activities are read through the todo controller. There are no source-defined endpoints to manually create, update, or delete activities.

### Get Todo Activities

Method: `GET`

Route: `/api/todos/{id}/activities`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes | Todo id. |

Query parameters: none

Request body: none

Response body:

```json
[
  {
    "id": 1,
    "type": "Created",
    "description": "Todo created",
    "createdAt": "2026-08-24T00:00:00Z"
  }
]
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `TODO_NOT_FOUND`
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The todo must belong to the authenticated user.
- A todo owned by another user is treated as not found.
- Activities are returned newest first by `createdAt` descending.

## TodoCategory Endpoints

All todo category endpoints require an authenticated JWT.

### Get Categories

Method: `GET`

Route: `/api/todo-categories`

Authorization: authenticated user

Path/query parameters: none

Request body: none

Response body:

```json
[
  {
    "id": 1,
    "name": "Work"
  }
]
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- Only categories where `userId` equals the authenticated user id are returned.

### Get Category

Method: `GET`

Route: `/api/todo-categories/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body: none

Response body:

```json
{
  "id": 1,
  "name": "Work"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `CATEGORY_NOT_FOUND`
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The category must belong to the authenticated user.
- A category owned by another user is treated as not found.

### Create Category

Method: `POST`

Route: `/api/todo-categories`

Authorization: authenticated user

Path/query parameters: none

Request body:

```json
{
  "name": "Work"
}
```

Response body:

```json
{
  "message": "Category created successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `400 Bad Request` for malformed JSON or model binding errors
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- `userId` is set from the authenticated user.
- Database constraints define `name` as required with max length `100`.
- The service does not check for duplicate category names.

### Update Category

Method: `PUT`

Route: `/api/todo-categories/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters: none

Request body:

```json
{
  "name": "Personal"
}
```

Response body:

```json
{
  "message": "Category updated successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `CATEGORY_NOT_FOUND`
- `400 Bad Request` for malformed JSON or model binding errors
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The category must belong to the authenticated user.
- A category owned by another user is treated as not found.
- Database constraints define `name` as required with max length `100`.
- The service does not check for duplicate category names.

### Delete Category

Method: `DELETE`

Route: `/api/todo-categories/{id}`

Authorization: authenticated user

Path parameters:

| Name | Type | Required |
| --- | --- | --- |
| `id` | integer | yes |

Query parameters:

| Name | Type | Required | Default | Notes |
| --- | --- | --- | --- | --- |
| `deleteTodos` | boolean | no | `false` | Controls what happens to todos in the category. |

Request body: none

Response body:

```json
{
  "message": "Category deleted successfully"
}
```

Possible error codes:

- `401 Unauthorized` when the JWT is missing or invalid
- `404 Not Found`, `CATEGORY_NOT_FOUND`
- `500 Internal Server Error` if the authenticated token does not contain a parseable user id claim

Business rules:

- The category must belong to the authenticated user.
- A category owned by another user is treated as not found.
- If `deleteTodos=true`, todos in that category for the authenticated user are deleted.
- If `deleteTodos=false`, todos in that category for the authenticated user are kept and their `categoryId` is set to `null`.
- The category is deleted after todo handling.

## Ownership Rules

- Todo and category ownership is based on the authenticated JWT `ClaimTypes.NameIdentifier` claim.
- Todo list queries are scoped to `todo.userId == currentUserId`.
- Todo detail, update, status update, delete, and activity reads search by both todo id and current user id.
- Category list queries are scoped to `category.userId == currentUserId`.
- Category detail, update, and delete search by both category id and current user id.
- Category assignment during todo create/update only succeeds if the category belongs to the authenticated user.
- Cross-user todo and category access returns the same not-found errors as missing resources.

## Status Transition Rules

Same-status updates are allowed. Other valid transitions:

| Current status | Allowed new statuses |
| --- | --- |
| `Todo` | `InProgress`, `Done` |
| `InProgress` | `Todo`, `Done` |
| `Done` | `InProgress` |

Invalid transitions return:

```json
{
  "success": false,
  "statusCode": 409,
  "errorCode": "INVALID_TODO_STATUS_TRANSITION",
  "message": "Cannot change status from Done to Todo",
  "timestamp": "2026-08-24T00:00:00Z",
  "path": "/api/todos/1/status"
}
```

## Overdue and Completed-Late Logic

`isOverdue` on `TodoDto` is computed at mapping time:

- `true` when `dueDate` has a value, `dueDate < DateTime.UtcNow`, and `status != Done`.
- `false` when there is no due date, the due date is not in the past, or the todo is done.

`GET /api/todos?isOverdue=true` uses the same overdue rule in the database query.

`GET /api/todos?isOverdue=false` returns todos that meet any of these conditions:

- no `dueDate`
- `dueDate >= DateTime.UtcNow`
- `status == Done`

`completedAt` behavior:

- Set to current UTC time when status is changed to `Done`.
- Cleared when status is changed from `Done` to `InProgress`.
- Not changed by the general todo update endpoint.

`isCompletedLate` on `TodoDto` is computed at mapping time:

- `true` when `completedAt` has a value, `dueDate` has a value, and `completedAt > dueDate`.
- `false` otherwise.
