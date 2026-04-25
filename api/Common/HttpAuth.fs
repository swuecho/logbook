module HttpAuth

open System.Net
open System.Security.Claims
open Falco

let forbidden ctx =
    let message = "Access to the resource is forbidden."

    (Response.withStatusCode 403
     >> Response.ofJson
         {| code = HttpStatusCode.Forbidden
            message = message |})
        ctx

let getUserId (user: ClaimsPrincipal) =
    user.FindFirst(AppIdentity.userIdClaim).Value |> int
