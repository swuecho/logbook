module AdminHandlers

open Falco
open Database.Connection

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if ctx.User.IsInRole "admin" then
            let conn = ctx.GetNpgsqlConnection()

            Json.Response.ofJson (AdminService.usersWithDiaryCount conn) ctx
        else
            HttpAuth.forbidden ctx
