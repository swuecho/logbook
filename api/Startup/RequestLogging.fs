module RequestLogging

open System.Diagnostics
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

/// After JWT authentication, logs each request with structured fields: Method, Path, StatusCode, ElapsedMs, UserId.
let useMiddleware (app: IApplicationBuilder) =
    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
        task {
            let sw = Stopwatch.StartNew()
            let logFactory = context.RequestServices.GetRequiredService<ILoggerFactory>()
            let logger = logFactory.CreateLogger("Logbook.Request")

            try
                do! next.Invoke context
            finally
                sw.Stop()

                let userIdStr =
                    match HttpAuth.tryGetUserId context.User with
                    | None -> "anonymous"
                    | Some u -> string u

                logger.LogInformation(
                    "HTTP {RequestMethod} {RequestPath} {StatusCode} {ElapsedMs} {UserId}",
                    context.Request.Method,
                    (string context.Request.Path),
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    userIdStr
                )
        }

    app.Use(middleware)
