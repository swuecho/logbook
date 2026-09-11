module SessionService

open System
open Database

type SessionView =
    { Authenticated: bool
      UserId: int
      Role: string
      Issuer: string
      Audience: string
      CsrfToken: string
      ExpiresAt: DateTime }

type IssuedSession = { Jwt: string; View: SessionView }

let view (config: JwtService.JwtConfig) token userId isAdmin expiresAt =
    { Authenticated = true; UserId = userId
      Role = if isAdmin then AppIdentity.adminRole else AppIdentity.userRole
      Issuer = AppIdentity.jwtIssuer; Audience = config.Audience
      CsrfToken = SessionToken.csrf token; ExpiresAt = expiresAt }

let issue conn (config: JwtService.JwtConfig) userId isAdmin previousToken =
    let role = if isAdmin then AppIdentity.adminRole else AppIdentity.userRole
    let jwt = Token.generateToken userId role config.Secret config.Audience AppIdentity.jwtIssuer
    let previousHash = if String.IsNullOrEmpty(previousToken) then "" else SessionToken.hash previousToken
    SessionRepository.create conn userId (SessionToken.hash jwt.AccessToken) jwt.ExpiresAt previousHash
    { Jwt = jwt.AccessToken; View = view config jwt.AccessToken userId isAdmin jwt.ExpiresAt }

let authenticate (db: DbSession) config token userId =
    db.WithConnection(fun conn ->
        SessionRepository.authenticate conn (SessionToken.hash token) userId
        |> Option.map (fun s -> view config token s.UserId s.IsAdmin s.ExpiresAt))

let logout (db: DbSession) token =
    if not (String.IsNullOrEmpty token) then
        db.WithConnection(fun conn -> SessionRepository.revoke conn (SessionToken.hash token))
