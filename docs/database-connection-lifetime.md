# Database Connection Lifetime

This note compares the old per-request database connection middleware with the current `withConnection` helper in the API.

## Current Approach

The API now registers one `NpgsqlDataSource` during startup and opens short-lived connections from it when a handler needs database access.

```fsharp
let withConnection (httpContext: HttpContext) action =
    use conn = openConnection httpContext
    action conn
```

Handlers use it around the work that needs a connection:

```fsharp
withConnection ctx (fun conn ->
    Json.Response.ofJson (DiaryService.listDiaryIds conn userId) ctx)
```

`NpgsqlDataSource` is the long-lived, DI-owned object. Individual `NpgsqlConnection` values are opened, used, and disposed at the call site.

## Old Approach

The previous implementation used middleware to open one `NpgsqlConnection` for every request and store it in `HttpContext.Items`.

```fsharp
context.Items.["NpgsqlConnection"] <- connection
connection.Open()
return! next.Invoke context
```

Handlers then retrieved the connection from request state:

```fsharp
let conn = ctx.GetNpgsqlConnection()
```

That made every request carry a database connection, even requests that did not use the database.

## Comparison

| Area | Old per-request middleware | Current `withConnection` |
| --- | --- | --- |
| Lifetime | One connection opened for the full request | Connection opened only around database work |
| Ownership | Hidden in `HttpContext.Items` | Explicit at each handler call site |
| Dependency source | Custom request storage | ASP.NET Core DI via `NpgsqlDataSource` |
| Non-DB requests | Still opened a connection | Opens no connection |
| Failure mode | Missing item caused runtime failure | Missing DI registration fails clearly via service resolution |
| Testability | Handler depends on custom context mutation | Handler depends on smaller helper boundary |
| Npgsql pattern | Manual connection construction | Uses modern `NpgsqlDataSource` pooling/configuration |

## Why `withConnection` Is Better Here

The current API handlers are synchronous and each endpoint performs a small number of database operations. For that shape, a scoped helper is simpler and tighter than keeping a connection alive for the whole request.

The main improvement is that database lifetime now matches database usage. Static files, auth failures, and other non-DB paths do not allocate a connection. DB-using handlers show their dependency explicitly:

```fsharp
withConnection ctx (fun conn ->
    SomeService.doWork conn input)
```

This also removes the custom `HttpContext.Items` contract, which was easy to break because handlers and middleware had to agree on a string key.

## Tradeoffs

`withConnection` can open more than one connection inside a single request if a handler calls it multiple times. The current handlers avoid that by wrapping each endpoint once and passing `conn` down through services.

If an endpoint later needs a transaction across multiple service calls, keep one `withConnection` block around the whole operation and start the transaction inside it. Do not call `withConnection` separately for each step of a transactional workflow.

## Recommended Rule

Use one `withConnection` call per handler that needs database access. Open it after request parsing and authorization, then pass the connection into services and repositories.

For future async DB work, add an async equivalent, for example `withConnectionTask`, so connection disposal still remains centralized.
