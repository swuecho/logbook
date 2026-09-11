module SessionHttp

open System
open Microsoft.AspNetCore.Http

let private sessionKey = "logbook.session"
let cookie (ctx: HttpContext) =
    match ctx.Request.Cookies.TryGetValue(SessionToken.cookieName) with
    | true, token when token.Length <= 4096 -> token
    | _ -> ""

let private options () =
    CookieOptions(HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true)

let setCookie (ctx: HttpContext) (issued: SessionService.IssuedSession) =
    let settings = options ()
    settings.Expires <- Nullable(DateTimeOffset(issued.View.ExpiresAt))
    ctx.Response.Cookies.Append(SessionToken.cookieName, issued.Jwt, settings)
    ctx.Response.Cookies.Delete(SessionToken.bootstrapCookieName, options ())

let clearCookies (ctx: HttpContext) =
    ctx.Response.Cookies.Delete(SessionToken.cookieName, options ())
    ctx.Response.Cookies.Delete(SessionToken.bootstrapCookieName, options ())

let setSession (ctx: HttpContext) (view: SessionService.SessionView) = ctx.Items[sessionKey] <- view
let current (ctx: HttpContext) =
    match ctx.Items.TryGetValue(sessionKey) with
    | true, (:? SessionService.SessionView as view) -> Some view
    | _ -> None

let private bootstrap (ctx: HttpContext) =
    match ctx.Request.Cookies.TryGetValue(SessionToken.bootstrapCookieName) with
    | true, value when value.Length = 64 && value |> Seq.forall Uri.IsHexDigit -> value
    | _ -> ""

let expectedCsrf (ctx: HttpContext) =
    match current ctx with
    | Some session -> session.CsrfToken
    | None -> let value = bootstrap ctx in if value = "" then "" else SessionToken.csrf value

let bootstrapCsrf (ctx: HttpContext) =
    let existing = bootstrap ctx
    let value = if existing = "" then SessionToken.random () else existing
    let settings = options ()
    settings.MaxAge <- Nullable(TimeSpan.FromMinutes(20.0))
    ctx.Response.Cookies.Append(SessionToken.bootstrapCookieName, value, settings)
    SessionToken.csrf value

let expectedOrigin (ctx: HttpContext) =
    let configured = Environment.GetEnvironmentVariable("LOGBOOK_PUBLIC_ORIGIN")
    if not (String.IsNullOrWhiteSpace configured) then configured.TrimEnd('/')
    else
        // Production reverse proxies must preserve Host. Forwarded headers are
        // not trusted implicitly. Use an explicit public origin when rewriting Host.
        let local = ctx.Request.Host.Host = "localhost" || ctx.Request.Host.Host = "127.0.0.1" || ctx.Request.Host.Host = "[::1]"
        let scheme = if local then ctx.Request.Scheme else "https"
        $"{scheme}://{ctx.Request.Host}"

let originAllowed (ctx: HttpContext) =
    let origin = ctx.Request.Headers.Origin.ToString()
    let safe = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method)
    ctx.Request.Headers["Sec-Fetch-Site"].ToString() <> "cross-site"
    && (if safe && origin = "" then true else origin = expectedOrigin ctx)
