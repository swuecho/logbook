module AuthService

open Database
open Logbook

type Login = { Username: string; Password: string }

type AccessTokenResponse =
    { AccessToken: string
      ExpiresIn: int }

type LoginResult =
    | LoginSucceeded of AccessTokenResponse
    | LoginFailed of ApiError

let private createNewUser conn email password =
    let passwordHash = Auth.generatePasswordHash password
    AuthUserRepository.create conn email passwordHash "" "" email false false

let private loginFailed = LoginFailed HttpError.invalidCredentials

let private roleForUser user =
    if user.IsSuperuser then
        AppIdentity.adminRole
    else
        AppIdentity.userRole

let private tokenResponse userId role jwtKey audience =
    let jwt = Token.generateToken userId role jwtKey audience AppIdentity.jwtIssuer

    { AccessToken = jwt.AccessToken
      ExpiresIn = jwt.ExpiresIn }

let private tryLoginExistingUser conn credentials (jwtConfig: JwtService.JwtConfig) =
    match AuthUserRepository.tryGetByEmail conn credentials.Username with
    | None -> None
    | Some user ->
        if Auth.validatePassword credentials.Password user.Password then
            AuthUserRepository.updateLastLogin conn user.Id
            Some(LoginSucceeded(tokenResponse user.Id (roleForUser user) jwtConfig.Secret jwtConfig.Audience))
        else
            Some loginFailed

let private createAndLoginUser conn credentials (jwtConfig: JwtService.JwtConfig) =
    let authUser = createNewUser conn credentials.Username credentials.Password
    LoginSucceeded(tokenResponse authUser.Id AppIdentity.userRole jwtConfig.Secret jwtConfig.Audience)

let loginOrRegister (db: DbSession) (jwtConfig: JwtService.JwtConfig) (credentials: Login) =
    db.WithConnection(fun conn ->
        match tryLoginExistingUser conn credentials jwtConfig with
        | Some result -> result
        | None -> createAndLoginUser conn credentials jwtConfig)
