module HandlerResponse

open Falco

let json ctx value =
    Json.Response.ofJson value ctx

let jsonHandler value =
    Json.Response.ofJson value

let jsonWithStatus statusCode value =
    Response.withStatusCode statusCode
    >> Json.Response.ofJson value

