module HttpAuth

open System.Net
open System.Security.Claims
open Falco

let forbidden =
    let message = "Access to the resource is forbidden."

    Response.withStatusCode 403
    >> Response.ofJson
        {| code = HttpStatusCode.Forbidden
           message = message |}

let AuthRequired h = Request.ifAuthenticated h forbidden

let getUserId (user: ClaimsPrincipal) =
    user.FindFirst("user_id").Value |> int
