module HttpAuth

open System.Text.Json
open System.Net
open Microsoft.AspNetCore.Http
open Falco

let forbidden =
    let message = "Access to the resource is forbidden."

    Response.withStatusCode 403
    >> Response.ofJson
        {| code = HttpStatusCode.Forbidden
           message = message |}

let AuthRequired h = Request.ifAuthenticated h forbidden


module Authentication =
    let isAuthenticated (context: HttpContext) =
        context.User.Identity.IsAuthenticated = true

    let Middleware (next: RequestDelegate) (context: HttpContext) =
        // check path start with /api
        if context.Request.Path.StartsWithSegments(PathString("/api")) then
            if isAuthenticated context then
                next.Invoke context
            else
                context.Response.StatusCode <- 401
                context.Response.WriteAsync "Unauthorized"
        else
            next.Invoke context
