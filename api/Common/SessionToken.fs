module SessionToken

open System
open System.Security.Cryptography
open System.Text

let cookieName = "__Host-logbook-session"
let bootstrapCookieName = "__Host-logbook-csrf"
let csrfHeader = "X-CSRF-Token"
let lifetime = TimeSpan.FromHours(12.0)
let random () = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
let hash (token: string) = SHA256.HashData(Encoding.UTF8.GetBytes(token)) |> Convert.ToHexString
// Domain separation prevents exposing the stored token hash as the CSRF value.
let csrf token = hash ("logbook-csrf-v1:" + token)
let equals (expected: string) (actual: string) =
    not (String.IsNullOrEmpty(expected)) && not (isNull actual)
    && expected.Length = actual.Length
    && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual))
