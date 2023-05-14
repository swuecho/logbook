module Note

open System.Net
open Microsoft.AspNetCore.Http
open Falco
open System.Security.Claims
open Npgsql

let forbidden =
    let message = "Access to the resource is forbidden."

    Response.ofJson
        {| code = HttpStatusCode.Forbidden
           message = message |}

let getUserId (user: ClaimsPrincipal) = int (user.FindFirst("user_id").Value)


let AuthRequired h = Request.ifAuthenticated h forbidden

/// from async objec to json response


type HttpContext with

    member this.getNpgsql() =
        // implementation of your method
        match this.Items.["NpgsqlConnection"] with
        | :? NpgsqlConnection as connection -> connection
        | _ -> failwith "can not get connection"


// https://github.com/pimbrouwers/Falco/pull/41
// do not cache result
let noteAllPartSlow: HttpHandler =
    fun ctx ->
        let user_id = getUserId ctx.User
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Diary.ListDiaryByUserID conn user_id
                |> List.map (Jieba.freqsOfNote conn)
                |> Json.Response.ofJson)
            ctx

let noteAllPart: HttpHandler =
    fun ctx ->
        // refresh note summary
        let user_id = int (ctx.User.FindFirst("user_id").Value)
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Jieba.refreshSummary conn user_id
                Summary.GetSummaryByUserId conn user_id |> Json.Response.ofJson)
            ctx


let getNoteById conn id user_id =
    Diary.DiaryByUserIDAndID conn { Id = id; UserId = user_id }


let noteByIdPart: HttpHandler =
    fun ctx ->
        let conn = ctx.getNpgsql ()

        let getSurvey (route: RouteCollectionReader) =
            let user_id = getUserId ctx.User
            let note_id = route.GetString("id", "")
            getNoteById conn note_id user_id

        Request.mapRoute getSurvey Json.Response.ofJson ctx

let addNotePart: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let user_id = int (ctx.User.FindFirst("user_id").Value)
                let conn = ctx.getNpgsql ()

                Diary.AddNote
                    conn
                    { Id = note.Id
                      UserId = user_id
                      Note = note.Note }
                |> Json.Response.ofJson)
            ctx

let noteByIdPartDebug: HttpHandler =
    fun ctx ->
        let principal = ctx.User

        for claim in principal.Claims do
            printfn "%s" ("CLAIM TYPE: " + claim.Type + "; CLAIM VALUE: " + claim.Value)

        printfn "%A" ctx.User |> ignore
        noteByIdPart ctx
