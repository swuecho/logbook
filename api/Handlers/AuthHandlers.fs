module AuthHandlers

open Falco
open Microsoft.Extensions.DependencyInjection

let private issued status (session: SessionService.IssuedSession): HttpHandler =
    fun ctx ->
        SessionHttp.setCookie ctx session
        HandlerResponse.jsonWithStatus status session.View ctx

let login: HttpHandler =
    fun ctx ->
        let db = HandlerContext.dbSession ctx
        let config = ctx.RequestServices.GetRequiredService<JwtService.JwtConfig>()
        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.login db config (SessionHttp.cookie ctx) credentials with
                | AuthService.LoginSucceeded session -> issued 200 session
                | AuthService.LoginFailed err -> HandlerResponse.clientError 401 err) ctx

let register: HttpHandler =
    fun ctx ->
        let db = HandlerContext.dbSession ctx
        let config = ctx.RequestServices.GetRequiredService<JwtService.JwtConfig>()
        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.register db config (SessionHttp.cookie ctx) credentials with
                | AuthService.RegisterSucceeded session -> issued 201 session
                | AuthService.RegisterFailed err -> HandlerResponse.clientError 409 err) ctx

let session: HttpHandler =
    fun ctx ->
        match SessionHttp.current ctx with
        | Some view -> HandlerResponse.json ctx view
        | None -> HandlerResponse.json ctx {| authenticated = false; csrfToken = SessionHttp.bootstrapCsrf ctx |}

let logout: HttpHandler =
    fun ctx ->
        SessionService.logout (HandlerContext.dbSession ctx) (SessionHttp.cookie ctx)
        SessionHttp.clearCookies ctx
        HandlerResponse.json ctx {| message = "Logged out successfully" |}
