module HttpAuth

open System
open System.Net
open System.Security.Claims
open Falco

let private errorResponse statusCode code message ctx =
    (Response.withStatusCode statusCode
     >> Response.ofJson
         {| code = code
            message = message |})
        ctx

let unauthorized ctx =
    errorResponse
        401
        HttpStatusCode.Unauthorized
        "Authentication is required to access this resource."
        ctx

let forbidden ctx =
    errorResponse
        403
        HttpStatusCode.Forbidden
        "Access to the resource is forbidden."
        ctx

let tryGetUserId (user: ClaimsPrincipal) =
    match user.FindFirst(AppIdentity.userIdClaim) with
    | null -> None
    | claim ->
        match Int32.TryParse(claim.Value) with
        | true, userId -> Some userId
        | false, _ -> None

let getUserId (user: ClaimsPrincipal) =
    match tryGetUserId user with
    | Some userId -> userId
    | None -> invalidOp $"Authenticated user is missing a valid {AppIdentity.userIdClaim} claim."
