module HandlerContext

open Database
open Microsoft.AspNetCore.Http

type AuthenticatedRequest =
    { UserId: int
      DbSession: DbSession }

let userId (ctx: HttpContext) =
    HttpAuth.getUserId ctx.User

let dbSession (ctx: HttpContext) =
    Database.Connection.dbSession ctx

let authenticated (ctx: HttpContext) =
    { UserId = userId ctx
      DbSession = dbSession ctx }

let isAdmin (ctx: HttpContext) =
    ctx.User.IsInRole AppIdentity.adminRole

