module HttpAuth

open System
open System.Security.Claims
open Logbook

let unauthorized ctx =
    HandlerResponse.clientError 401 HttpError.authenticationRequired ctx

let forbidden ctx =
    HandlerResponse.clientError 403 HttpError.accessDenied ctx

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
