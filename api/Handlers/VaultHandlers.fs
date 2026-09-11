module VaultHandlers

open System
open System.IO
open System.Text.Json
open Falco

let private noStore (ctx: Microsoft.AspNetCore.Http.HttpContext) =
    ctx.Response.Headers["Cache-Control"] <- "no-store"
    ctx.Response.Headers["Pragma"] <- "no-cache"

let private error status message =
    HandlerResponse.jsonWithStatus status {| message = message |}

let get: HttpHandler =
    fun ctx ->
        noStore ctx
        match VaultService.get (HandlerContext.dbSession ctx) (HandlerContext.userId ctx) with
        | Some snapshot -> HandlerResponse.json ctx snapshot
        | None -> error 404 "No vault has been created." ctx

let save: HttpHandler =
    fun ctx ->
        task {
            noStore ctx
            // Bound chunked requests too, before JSON deserialization.
            use buffer = new MemoryStream()
            let chunk = Array.zeroCreate<byte> 8192
            let mutable reading = true
            while reading && buffer.Length <= 1600000L do
                let! count = ctx.Request.Body.ReadAsync(chunk.AsMemory(), ctx.RequestAborted)
                if count = 0 then reading <- false
                else buffer.Write(chunk, 0, count)
            if buffer.Length > 1600000L then
                return! error 413 "Vault is too large." ctx
            else
                let parsed =
                    try
                        let value = JsonSerializer.Deserialize<VaultService.Mutation>(buffer.ToArray().AsSpan(), Json.Options.serializerOptions)
                        if obj.ReferenceEquals(value, null) then None else Some value
                    with :? JsonException -> None
                match parsed with
                | None -> return! error 400 "Invalid encrypted vault." ctx
                | Some mutation ->
                    match VaultService.save (HandlerContext.dbSession ctx) (HandlerContext.userId ctx) mutation with
                    | VaultService.Saved snapshot -> return! HandlerResponse.json ctx snapshot
                    | VaultService.Conflict -> return! error 409 "Vault changed in another session. Lock and unlock to load the latest version." ctx
                    | VaultService.Invalid -> return! error 400 "Invalid encrypted vault." ctx
        }
