namespace Json

open System.Text.Json

module Convert =

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    let toJson v =
        JsonSerializer.Serialize(v, jsonOptions)

//let fromJson<'a> json =
//    JsonSerializer.Deserialize<'a>(json, jsonOptions)
