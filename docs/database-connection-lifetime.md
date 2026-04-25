# Database Connection Lifetime

This note compares the database connection approaches used in the API and explains why the current `DbSession` boundary is preferred.

## Current Approach: `DbSession`

The API registers one long-lived `NpgsqlDataSource` during startup, then registers a small `DbSession` wrapper in dependency injection.

```fsharp
type DbSession(dataSource: NpgsqlDataSource) =
    member _.WithConnection(action: NpgsqlConnection -> 'T) : 'T =
        use conn = dataSource.OpenConnection()
        action conn

    member _.WithTransaction(action: NpgsqlConnection -> 'T) : 'T =
        use conn = dataSource.OpenConnection()
        use transaction = conn.BeginTransaction()

        try
            let result = action conn
            transaction.Commit()
            result
        with _ ->
            transaction.Rollback()
            reraise ()
```

Handlers resolve the session and pass it to services:

```fsharp
Json.Response.ofJson (DiaryService.listDiaryIds (dbSession ctx) userId) ctx
```

Services own connection lifetime:

```fsharp
let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listIdsByUserId conn userId)
```

Write workflows that update more than one database concern use a transaction boundary:

```fsharp
let saveDiary (db: DbSession) userId note =
    db.WithTransaction(fun conn ->
        let saved = DiaryRepository.addOrUpdate conn note.NoteId userId note.Note
        SearchIndexService.updateSearchIndex conn saved.NoteId saved.UserId saved.Note
        saved)
```

This keeps HTTP handlers focused on HTTP concerns while services and repositories own application/data access work.

## Previous Approach: Handler-Level `withConnection`

The intermediate approach registered `NpgsqlDataSource` in DI but exposed a helper directly to handlers:

```fsharp
withConnection ctx (fun conn ->
    Json.Response.ofJson (DiaryService.listDiaryIds conn userId) ctx)
```

This fixed the biggest issue from the old middleware: connections were opened only when needed. However, handlers still managed infrastructure lifetime and services still accepted raw `NpgsqlConnection` values for top-level operations.

## Original Approach: Per-Request Middleware

The first implementation opened one `NpgsqlConnection` for every request and stored it in `HttpContext.Items`.

```fsharp
context.Items.["NpgsqlConnection"] <- connection
connection.Open()
return! next.Invoke context
```

Handlers retrieved it from request state:

```fsharp
let conn = ctx.GetNpgsqlConnection()
```

That meant every request carried a database connection, including requests that did not use the database.

## Comparison

| Area | Per-request middleware | Handler `withConnection` | Current `DbSession` |
| --- | --- | --- | --- |
| Connection lifetime | Full request | Around handler DB work | Around service DB work |
| Handler responsibility | Reads hidden request item | Opens connection scope | Resolves session only |
| Service responsibility | Uses raw connection | Uses raw connection | Owns DB operation boundary |
| Dependency source | `HttpContext.Items` string key | DI `NpgsqlDataSource` | DI `DbSession` |
| Non-DB requests | Opened a connection | No connection | No connection |
| Testability | Hard to isolate | Better, but handlers know DB lifetime | Best current shape |
| Transaction path | Ad hoc | Handler must coordinate | Can add `DbSession.WithTransaction` |

## Why `DbSession` Is Better

`DbSession` keeps the important behavior from `withConnection`: short-lived connections opened from a pooled `NpgsqlDataSource`. The difference is ownership. Handlers no longer decide when to open a database connection; they delegate application work to services.

This gives cleaner boundaries:

- Handlers parse requests, read route/query/body data, and format responses.
- Services describe application operations and connection scope.
- Repositories call generated SQL modules.
- `NpgsqlDataSource` remains a DI-owned infrastructure object.

## Tradeoffs

`DbSession` is still a small infrastructure abstraction, not a full unit-of-work framework. That is intentional for this app. It keeps the code simple while centralizing connection lifetime.

If a service operation needs multiple repository calls to share one transaction, use `WithTransaction` and keep the whole workflow inside one service operation. Avoid opening separate sessions for steps that must commit or roll back together.

`Npgsql.FSharp` also has `Sql.executeTransaction`, which is a good fit for a batch of SQL statements and parameter sets:

```fsharp
connectionString
|> Sql.connect
|> Sql.executeTransaction
    [
        "INSERT INTO ... VALUES (@number)", [
            [ "@number", Sql.int 1 ]
            [ "@number", Sql.int 2 ]
        ]
        "UPDATE ... SET meta = @meta", [
            [ "@meta", Sql.text value ]
        ]
    ]
```

That is not the best fit for the current diary save workflow because the app uses generated query functions, needs the saved diary row returned from the first query, and then uses that result to update the search index. The Npgsql.FSharp docs recommend creating an explicit `NpgsqlTransaction` for arbitrary code between SQL calls, which is what `DbSession.WithTransaction` centralizes.

## Recommended Rule

Handlers should not accept or open `NpgsqlConnection` values. A handler may resolve `DbSession` from the request service provider and pass it to a service. Services should use one `DbSession.WithConnection` block for read-only single-operation workflows, or one `DbSession.WithTransaction` block for multi-step writes. Raw connections should only be passed to repositories or lower-level helpers.
