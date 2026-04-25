namespace Json

open System.Text.Json

module Options =
    let serializerOptions =
        let options = JsonSerializerOptions()
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.PropertyNameCaseInsensitive <- true
        options

module Convert =
    let toJson v =
        JsonSerializer.Serialize(v, Options.serializerOptions)

module Request =
    open Falco

    let mapJson handler ctx =
        Request.mapJsonOptions Options.serializerOptions handler ctx

module Response =
    open Falco

    let ofJsonTask (taskObj: System.Threading.Tasks.Task<'a>) : HttpHandler =
        fun ctx ->
            task {
                let! obj = taskObj
                return! Response.ofJsonOptions Options.serializerOptions obj ctx
            }

    let ofJson v = Response.ofJsonOptions Options.serializerOptions v
