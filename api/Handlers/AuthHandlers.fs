module AuthHandlers

open Falco
open Microsoft.Extensions.DependencyInjection

let login: HttpHandler =
    fun ctx ->
        let dbSession = HandlerContext.dbSession ctx
        let jwtConfig = ctx.RequestServices.GetRequiredService<JwtService.JwtConfig>()

        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.login dbSession jwtConfig credentials with
                | AuthService.LoginSucceeded token -> HandlerResponse.jsonHandler token
                | AuthService.LoginFailed err -> HandlerResponse.clientError 401 err)
            ctx

let register: HttpHandler =
    fun ctx ->
        let dbSession = HandlerContext.dbSession ctx
        let jwtConfig = ctx.RequestServices.GetRequiredService<JwtService.JwtConfig>()

        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.register dbSession jwtConfig credentials with
                | AuthService.RegisterSucceeded token -> HandlerResponse.jsonWithStatus 201 token
                | AuthService.RegisterFailed err -> HandlerResponse.clientError 409 err)
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        {| userId = userId
           message = "Logged out successfully" |}
        |> HandlerResponse.json ctx
