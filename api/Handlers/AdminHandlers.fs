module AdminHandlers

open System
open Falco
open Logbook

let private requireAdmin handler : HttpHandler =
    fun ctx ->
        if HandlerContext.isAdmin ctx then
            handler ctx
        else
            HttpAuth.forbidden ctx

let usersWithDiaryCount: HttpHandler =
    requireAdmin (fun ctx ->
        let dbSession = HandlerContext.dbSession ctx
        AdminService.usersWithDiaryCount dbSession |> HandlerResponse.json ctx)

let deleteUser: HttpHandler =
    requireAdmin (fun ctx ->
        let dbSession = HandlerContext.dbSession ctx
        let requestingUserId = HandlerContext.userId ctx
        let rawUserId = HandlerContext.routeValue "id" "" ctx

        match Int32.TryParse(rawUserId) with
        | false, _ -> HandlerResponse.clientError 400 HttpError.invalidUserId ctx
        | true, targetUserId ->
            match AdminService.deleteUser dbSession requestingUserId targetUserId with
            | AdminService.UserDeleted ->
                {| userId = targetUserId
                   deactivated = true |}
                |> HandlerResponse.json ctx
            | AdminService.UserNotFound -> HandlerResponse.clientError 404 HttpError.userNotFound ctx
            | AdminService.CannotDeleteSelf -> HandlerResponse.clientError 400 HttpError.cannotDeleteSelf ctx)
