module HttpAuth

open System.Text.Json
open System.Net
open Microsoft.AspNetCore.Http
open Falco

let forbidden =
    let message = "Access to the resource is forbidden."
    Response.withStatusCode 403  >>
    Response.ofJson
        {| code = HttpStatusCode.Forbidden
           message = message |}

let AuthRequired h = Request.ifAuthenticated h forbidden
