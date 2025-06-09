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
        fun ctx ->
            task {
                let! obj = taskObj
                return! Response.ofJsonOptions jsonOptions obj ctx
            }

    let ofJson v = Response.ofJsonOptions jsonOptions v
