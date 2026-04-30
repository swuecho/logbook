module HandlerContext

open Database
open Falco
open Microsoft.AspNetCore.Http

type AuthenticatedRequest =
    { UserId: int
      DbSession: DbSession }

let userId (ctx: HttpContext) =
    HttpAuth.getUserId ctx.User

let dbSession (ctx: HttpContext) =
    Connection.dbSession ctx

let authenticated (ctx: HttpContext) =
    { UserId = userId ctx
      DbSession = dbSession ctx }

let isAdmin (ctx: HttpContext) =
    ctx.User.IsInRole AppIdentity.adminRole

let routeValue name defaultValue (ctx: HttpContext) =
    let route = Request.getRoute ctx
    route.GetString(name, defaultValue)

let queryValue name defaultValue (ctx: HttpContext) =
    match ctx.Request.Query.TryGetValue(name) with
    | true, value -> value.ToString()
    | false, _ -> defaultValue

