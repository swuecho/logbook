# Using `dbSession`

This tutorial explains how API handlers get a database session and how services should use it to run database work.

## What `dbSession` Is

`dbSession` is a helper function in `Database.Connection`:

```fsharp
let dbSession (httpContext: HttpContext) =
    httpContext.RequestServices.GetRequiredService<DbSession>()
```

It takes the current ASP.NET `HttpContext`, reads the request service provider, and resolves the app's registered `DbSession`.

Most handlers should go through `HandlerContext` instead of resolving the session directly:

```fsharp
let listDiaryIds: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId)
            ctx
```

`HandlerContext.authenticated` bundles request data commonly needed by authenticated handlers:

```fsharp
type AuthenticatedRequest =
    { UserId: int
      DbSession: DbSession }
```

`HandlerContext.dbSession` is still available as a smaller helper:

```fsharp
let dbSession (ctx: HttpContext) =
    Database.Connection.dbSession ctx
```

You can still call `Database.Connection.dbSession` directly if needed:

```fsharp
Database.Connection.dbSession ctx
```

If a file opens `Database.Connection`, it can also call the helper without the module prefix:

```fsharp
open Database.Connection

dbSession ctx
```

## Where It Comes From

At startup, the app creates one long-lived `NpgsqlDataSource`:

```fsharp
let dataSource = Database.Connection.createDataSource Database.Config.connStr
```

Then it registers database services in dependency injection:

```fsharp
builder.Services |> AppStartup.addDatabase dataSource |> ignore
```

The actual registration is:

```fsharp
let addDatabase (dataSource: NpgsqlDataSource) (services: IServiceCollection) =
    services.AddSingleton<NpgsqlDataSource>(dataSource) |> ignore
    services.AddSingleton<DbSession>(fun _ -> DbSession(dataSource)) |> ignore
    services
```

That means every request can resolve `DbSession` through `ctx.RequestServices`.

## The Request Flow

The usual flow is:

1. A route invokes a handler.
2. The handler receives `ctx: HttpContext`.
3. The handler reads HTTP data, such as route values, query params, body JSON, or the current user.
4. The handler calls `HandlerContext.authenticated ctx` and passes the request context values to a service.
5. The service uses `DbSession.WithConnection` or `DbSession.WithTransaction`.
6. Repositories receive a raw `NpgsqlConnection` and execute SQL.

Example:

```fsharp
Json.Response.ofJson (DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId) ctx
```

This evaluates as:

```fsharp
let requestContext = HandlerContext.authenticated ctx
let result = DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId
Json.Response.ofJson result ctx
```

## Use `WithConnection` For Reads

Use `WithConnection` when the service needs one short-lived connection and does not need an explicit transaction.

```fsharp
let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn ->
        DiaryRepository.listIdsByUserId conn userId)
```

The connection is opened before the function runs and disposed afterward:

```fsharp
member _.WithConnection(action: NpgsqlConnection -> 'T) : 'T =
    use conn = dataSource.OpenConnection()
    action conn
```

Use this for read-only operations and simple single-step database work.

## Use `WithTransaction` For Multi-Step Writes

Use `WithTransaction` when several database operations must commit or roll back together.

```fsharp
let saveDiary (db: DbSession) userId (note: Diary) =
    db.WithTransaction(fun conn ->
        let saved = DiaryRepository.addOrUpdate conn note.NoteId userId note.Note
        SearchIndexService.updateSearchIndex conn saved.NoteId saved.UserId saved.Note
        saved)
```

If any operation inside the function fails, `DbSession` rolls the transaction back and re-raises the exception.

## Adding A New Endpoint

Start with the handler:

```fsharp
module MyHandlers

open Falco
open Microsoft.AspNetCore.Http

let getSomething: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (MyService.getSomething requestContext.DbSession requestContext.UserId)
            ctx
```

Then write the service function so it accepts `DbSession`:

```fsharp
module MyService

open Database

let getSomething (db: DbSession) userId =
    db.WithConnection(fun conn ->
        MyRepository.getSomething conn userId)
```

Keep raw `NpgsqlConnection` values in repositories or lower-level helpers:

```fsharp
module MyRepository

let getSomething conn userId =
    // call generated SQL or Npgsql.FSharp query code here
    failwith "example"
```

## Rules Of Thumb

- Authenticated handlers should usually use `HandlerContext.authenticated ctx`, then pass its `DbSession` and `UserId` to services.
- Services should decide whether the operation needs `WithConnection` or `WithTransaction`.
- Repositories should accept raw `NpgsqlConnection` values.
- Do not open database connections directly in handlers.
- Do not store `NpgsqlConnection` in `HttpContext.Items`.
- Use one `WithTransaction` block when multiple writes must succeed or fail together.

