namespace Service

open System.Text.Json
open System.Net
open Microsoft.AspNetCore.Http
open Falco

module Note =
    let forbidden =
        let message = "Access to the resource is forbidden."

        Response.ofJson
            {| code = HttpStatusCode.Forbidden
               message = message |}

    let AuthRequired h = Request.ifAuthenticated h forbidden

    /// from async objec to json response


    // https://github.com/pimbrouwers/Falco/pull/41
    // do not cache result
    let noteAllPartSlow: HttpHandler =
        Request.mapRoute (ignore) (fun _ ->
            Query.Note.getNoteAll ()
            |> List.map Summary.Jieba.freqsOfNote
            |> Json.Response.ofJson)

    let noteAllPart: HttpHandler =
        // refresh note summary
        Request.mapRoute (ignore) (fun _ ->
            Summary.Jieba.refreshSummary ()
            Query.Note.getSummaryAll () |> Json.Response.ofJson)



    let getNoteById id = Query.Note.getNoteById id


    let noteByIdPart: HttpHandler =
        let getSurvey (route: RouteCollectionReader) =
            route.GetString("id", "") |> getNoteById

        Request.mapRoute getSurvey Json.Response.ofJsonTask


    let putNote note = Query.Note.AddNote note


    let addNotePart: HttpHandler =
        fun ctx ->
            Request.mapJson
                (fun (note: Models.Note) ->
                    let user_id = int (ctx.User.FindFirst("user_id").Value)
                    let noteWithUser = { note with UserId = user_id }
                    putNote noteWithUser |> Json.Response.ofJsonTask)
                ctx

    let noteByIdPartDebug: HttpHandler =
        fun ctx ->
            let principal = ctx.User

            for claim in principal.Claims do
                printfn "%s" ("CLAIM TYPE: " + claim.Type + "; CLAIM VALUE: " + claim.Value)

            printfn "%A" ctx.User |> ignore
            noteByIdPart ctx
