module AuthHandlers

open Falco
open Database.Connection

let login: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (credentials: AuthService.Login) ->
                let conn = ctx.GetNpgsqlConnection()

                match AuthService.login conn credentials with
                | AuthService.LoginSucceeded token -> Json.Response.ofJson token
                | AuthService.LoginFailed failure ->
                    Response.withStatusCode (int failure.Code)
                    >> Json.Response.ofJson
                        {| code = failure.Code
                           message = failure.Message |})
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = HttpAuth.getUserId ctx.User

        Json.Response.ofJson
            {| userId = userId
               message = "Logged out successfully" |}
            ctx
