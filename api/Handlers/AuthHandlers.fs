module AuthHandlers

open Falco

let login: HttpHandler =
    fun ctx ->
        let dbSession = HandlerContext.dbSession ctx

        Json.Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.loginOrRegister dbSession credentials with
                | AuthService.LoginSucceeded token -> HandlerResponse.jsonHandler token
                | AuthService.LoginFailed failure ->
                    HandlerResponse.jsonWithStatus
                        (int failure.Code)
                        {| code = failure.Code
                           message = failure.Message |})
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        {| userId = userId
           message = "Logged out successfully" |}
        |> HandlerResponse.json ctx
