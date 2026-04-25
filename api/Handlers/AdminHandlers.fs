module AdminHandlers

open Falco
open Database.Connection

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if ctx.User.IsInRole "admin" then
            Json.Response.ofJson (AdminService.usersWithDiaryCount (dbSession ctx)) ctx
        else
            HttpAuth.forbidden ctx
