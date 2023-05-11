namespace Service

open System.IO
open System.Text.Json


open Falco

module Note =
    open System
    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    /// from async objec to json response
    let ofJsonTask
            (taskObj: System.Threading.Tasks.Task<'a>) : HttpHandler =
            let jsonTaskHandler : HttpHandler = fun ctx ->
                task {
                    let! obj = taskObj
                    use str = new MemoryStream()
                    do! JsonSerializer.SerializeAsync(str, obj, options = jsonOptions)
                    let bytes = str.ToArray()
                    let byteLen = bytes.Length
                    ctx.Response.ContentLength <- Nullable<int64>(byteLen |> int64)
                    let! _ = ctx.Response.BodyWriter.WriteAsync(ReadOnlyMemory<byte>(bytes))
                    return()
                }
            Response.withContentType "application/json; charset=utf-8"
            >> jsonTaskHandler

    let ResponseOfJson v = Response.ofJsonOptions jsonOptions v

    // https://github.com/pimbrouwers/Falco/pull/41
    // do not cache result
    let noteAllPartSlow: HttpHandler =
        Request.mapRoute (ignore) (fun _ ->
            Query.Note.getNoteAll () |> List.map Summary.Jieba.freqsOfNote |> ResponseOfJson)

    let noteAllPart: HttpHandler =
        // refresh note summary
        Request.mapRoute (ignore) (fun _ ->
            Summary.Jieba.refreshSummary ()
            Query.Note.getSummaryAll () |> ResponseOfJson)



    let getSurveyById id = Query.Note.getNoteById id


    let noteByIdPart: HttpHandler =
        let getSurvey (route: RouteCollectionReader) =
            route.GetString("id", "") |> getSurveyById

        Request.mapRoute getSurvey ofJsonTask


    let putNote note = Query.Note.AddNote note


    let addNotePart: HttpHandler =
        Request.mapJson (fun (note: Models.Note) -> putNote note |> ofJsonTask)
