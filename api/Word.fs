module Word

open System

let randomWords =
    [  ] // Add more words as needed

let getRandomWord =
    fun ctx ->
        let random = Random()
        let word = randomWords.[random.Next(randomWords.Length)]
        // refresh note summary
        Falco.Request.mapRoute (ignore) (fun _ -> {| word = word |} |> Json.Response.ofJson) ctx
