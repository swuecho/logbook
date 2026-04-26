module HandlerResponse

open Falco
open Logbook

let json ctx value =
    Json.Response.ofJson value ctx

let jsonHandler value =
    Json.Response.ofJson value

let jsonWithStatus statusCode value =
    Response.withStatusCode statusCode
    >> Json.Response.ofJson value

/// JSON error body `{ code, message }` with the given HTTP status.
let clientError (httpStatus: int) (err: ApiError) : HttpHandler =
    fun ctx -> (Response.withStatusCode httpStatus >> Json.Response.ofJson err) ctx

let plainText value =
    Response.ofPlainText value

