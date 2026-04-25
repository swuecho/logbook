module AdminHandlers

open Falco

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if HandlerContext.isAdmin ctx then
            Json.Response.ofJson (AdminService.usersWithDiaryCount (HandlerContext.dbSession ctx)) ctx
        else
            HttpAuth.forbidden ctx
