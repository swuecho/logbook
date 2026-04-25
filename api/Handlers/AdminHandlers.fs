module AdminHandlers

open Falco

let usersWithDiaryCount: HttpHandler =
    fun ctx ->
        if HandlerContext.isAdmin ctx then
            let dbSession = HandlerContext.dbSession ctx
            AdminService.usersWithDiaryCount dbSession |> HandlerResponse.json ctx
        else
            HttpAuth.forbidden ctx
