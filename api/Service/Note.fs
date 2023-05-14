namespace Service

open System.Text.Json


open Falco

module Note =

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



    let getSurveyById id = Query.Note.getNoteById id


    let noteByIdPart: HttpHandler =
        let getSurvey (route: RouteCollectionReader) =
            route.GetString("id", "") |> getSurveyById
        Request.mapRoute getSurvey Json.Response.ofJsonTask


    let putNote note = Query.Note.AddNote note


    let addNotePart: HttpHandler =
        Request.mapJson (fun (note: Models.Note) -> putNote note |> Json.Response.ofJsonTask)

    let noteByIdPartDebug: HttpHandler = fun ctx ->
        let principal = ctx.User
        for claim in principal.Claims do
            printfn "%s" ("CLAIM TYPE: " + claim.Type + "; CLAIM VALUE: " + claim.Value)
        printfn "%A" ctx.User |> ignore
        noteByIdPart ctx

        
