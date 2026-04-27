module AuthHandlers

open Falco
open Microsoft.Extensions.DependencyInjection

let login: HttpHandler =
    fun ctx ->
        let dbSession = HandlerContext.dbSession ctx
        let jwtConfig = ctx.RequestServices.GetRequiredService<JwtService.JwtConfig>()

        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.loginOrRegister dbSession jwtConfig credentials with
                | AuthService.LoginSucceeded token -> HandlerResponse.jsonHandler token
                | AuthService.LoginFailed err -> HandlerResponse.clientError 401 err)
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        {| userId = userId
           message = "Logged out successfully" |}
        |> HandlerResponse.json ctx
