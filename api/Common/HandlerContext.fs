module HandlerContext

open Microsoft.AspNetCore.Http

let userId (ctx: HttpContext) =
    HttpAuth.getUserId ctx.User

let dbSession (ctx: HttpContext) =
    Database.Connection.dbSession ctx

let isAdmin (ctx: HttpContext) =
    ctx.User.IsInRole "admin"

