module AuthHandlers

open Falco

let login: HttpHandler =
    fun ctx ->
        let dbSession = HandlerContext.dbSession ctx

        Request.mapJson
            (fun (credentials: AuthService.Login) ->
                match AuthService.login dbSession credentials with
                | AuthService.LoginSucceeded token -> Json.Response.ofJson token
                | AuthService.LoginFailed failure ->
                    Response.withStatusCode (int failure.Code)
                    >> Json.Response.ofJson
                        {| code = failure.Code
                           message = failure.Message |})
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson
            {| userId = userId
               message = "Logged out successfully" |}
            ctx
