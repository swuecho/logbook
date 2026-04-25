module AdminHandlers

open Falco
open Database.Connection

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if ctx.User.IsInRole "admin" then
            withConnection ctx (fun conn ->
                Json.Response.ofJson (AdminService.usersWithDiaryCount conn) ctx)
        else
            HttpAuth.forbidden ctx
