module AdminHandlers

open Falco

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if HandlerContext.isAdmin ctx then
            let dbSession = HandlerContext.dbSession ctx
            Json.Response.ofJson (AdminService.usersWithDiaryCount dbSession) ctx
        else
            HttpAuth.forbidden ctx
