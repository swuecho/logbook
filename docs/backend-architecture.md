# Backend Architecture

The API uses a small layered structure. Each layer has a narrow job so request handling, application workflows, and SQL access stay easy to follow.

## Layers

```text
Program.fs / Startup
  Configure infrastructure, middleware, authentication, routing, and app startup work.

Routing
  Map HTTP routes to handlers.

Handlers
  Read HTTP data, current user data, and request bodies. Call services and format responses.

Services
  Run application workflows. Decide whether database work needs a simple connection or a transaction.

Repositories
  Wrap generated SQL functions. Accept raw NpgsqlConnection values.

Infrastructure
  Own cross-cutting plumbing such as DbSession, NpgsqlDataSource, and JWT setup.

Common
  Hold shared request helpers, identity constants, JSON helpers, and utility functions.
```

## Request Flow

Most authenticated requests follow this path:

```text
ApiRoutes
  -> Handler
  -> HandlerContext.authenticated / HandlerContext.dbSession
  -> Service
  -> DbSession.WithConnection or DbSession.WithTransaction
  -> Repository
  -> generated SQL module
```

For example, a diary request should keep HTTP-specific work in the handler:

```fsharp
let listDiaryIds: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId)
            ctx
```

The service owns the database operation boundary:

```fsharp
let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn ->
        DiaryRepository.listIdsByUserId conn userId)
```

The repository receives the raw connection and delegates to query code:

```fsharp
let listIdsByUserId conn userId =
    Diary.ListIdsByUserId.run conn userId
```

## Handler Rules

Handlers should:

- Parse route values, query strings, JSON bodies, and user claims.
- Resolve request helpers through `HandlerContext`.
- Call one service function for the application operation.
- Return HTTP responses through `HandlerResponse` for standard JSON output.

Handlers should not:

- Open database connections directly.
- Start transactions.
- Call generated SQL modules directly.
- Hide business workflows inside response-building code.

## Service Rules

Services should:

- Accept `DbSession` as an argument.
- Use `WithConnection` for simple read or single-step database work.
- Use `WithTransaction` when multiple database changes must commit or roll back together.
- Call repositories or lower-level helpers with the connection.

If a function mutates state, its name should make that visible. For example, prefer `refreshAndListSummaries` over `listSummaries` when the implementation refreshes summary rows before reading them.

## Repository Rules

Repositories should:

- Accept `NpgsqlConnection`.
- Wrap generated query modules.
- Keep SQL-specific details away from handlers and services.

Repositories should stay thin. If a function coordinates multiple repository calls or business decisions, it belongs in a service.

## Startup Rules

Startup code should make infrastructure order clear:

- Create the shared `NpgsqlDataSource`.
- Initialize schema and startup data.
- Register `DbSession` and other services in DI.
- Configure authentication and CORS.
- Run the middleware pipeline once: routing, development errors, CORS, authentication, API auth gate, Falco routes, then static Vue files and SPA fallback.

If startup grows, split it by concern rather than adding more logic directly to `Program.fs`.

For a detailed explanation of DI registrations, lifetimes, runtime resolution, and the background worker graph, see [`.NET DI in This F# Backend`](./dotnet-di-in-fsharp.md).

## Identity Constants

Shared identity names live in `AppIdentity`:

```fsharp
let jwtAudienceName = "logbook"
let jwtIssuer = "logbook-swuecho.github.com"

let adminRole = "admin"
let userRole = "user"

let roleClaim = "role"
let userIdClaim = "user_id"
```

Use these constants instead of repeating role or JWT strings in handlers, services, or startup code.

## Auth Semantics

`AuthService.loginOrRegister` intentionally preserves the current product behavior: existing users are authenticated, and unknown emails are created as new users before returning a token. Keep that behavior explicit in names and docs so the `/api/login` route is not mistaken for a strict login-only endpoint.

## API Paths

Route path strings live in `ApiPaths`. `ApiRoutes` uses them to register handlers, and startup uses `ApiPaths.publicApiPaths` to decide which API routes can skip authentication.

This keeps route registration and middleware rules in sync:

```fsharp
post ApiPaths.login AuthHandlers.login
```

```fsharp
let private publicApiPaths =
    ApiPaths.publicApiPaths |> List.map PathString
```
