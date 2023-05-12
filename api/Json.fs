namespace Json

open System.Text.Json

module Convert =
    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    let toJson v =
        JsonSerializer.Serialize(v, jsonOptions)

module Response =
    open Falco
    open System
    open System.IO

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    let ofJsonTask (taskObj: System.Threading.Tasks.Task<'a>) : HttpHandler =
        let jsonTaskHandler: HttpHandler =
            fun ctx ->
                task {
                    let! obj = taskObj
                    use str = new MemoryStream()
                    do! JsonSerializer.SerializeAsync(str, obj, options = jsonOptions)
                    let bytes = str.ToArray()
                    let byteLen = bytes.Length
                    ctx.Response.ContentLength <- Nullable<int64>(byteLen |> int64)
                    let! _ = ctx.Response.BodyWriter.WriteAsync(ReadOnlyMemory<byte>(bytes))
                    return ()
                }

        Response.withContentType "application/json; charset=utf-8" >> jsonTaskHandler

    let ofJson v = Response.ofJsonOptions jsonOptions v
