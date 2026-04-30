module GlobalErrorHandler

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

/// Catches unhandled exceptions from downstream handlers, logs them with request
/// context, and returns a consistent JSON `{ code, message }` 500 response.
let useMiddleware (app: IApplicationBuilder) =
    let logFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>()
    let logger = logFactory.CreateLogger("Logbook.Error")

    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
        task {
            try
                do! next.Invoke context
            with ex ->
                let userIdStr =
                    match HttpAuth.tryGetUserId context.User with
                    | None -> "anonymous"
                    | Some u -> string u

                logger.LogError(
                    ex,
                    "Unhandled exception {RequestMethod} {RequestPath} {UserId}",
                    context.Request.Method,
                    string context.Request.Path,
                    userIdStr
                )

                // Avoid writing to a response that has already started.
                if not context.Response.HasStarted then
                    context.Response.StatusCode <- 500
                    context.Response.ContentType <- "application/json"

                    let body =
                        System.Text.Json.JsonSerializer.Serialize(Logbook.HttpError.internalError)

                    do! context.Response.WriteAsync(body)
        }

    app.Use middleware
