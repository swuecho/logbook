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

type RegisterResult =
    | RegisterSucceeded of AccessTokenResponse
    | RegisterFailed of ApiError

let private createNewUser conn email password =
    let passwordHash = Auth.generatePasswordHash password
    AuthUserRepository.create conn email passwordHash "" "" email false false

let private loginFailed = LoginFailed HttpError.invalidCredentials
let private registerFailed = RegisterFailed HttpError.emailAlreadyRegistered

let private roleForUser user =
    if user.IsSuperuser then
        AppIdentity.adminRole
    else
        AppIdentity.userRole

let private tokenResponse userId role jwtKey audience =
    let jwt = Token.generateToken userId role jwtKey audience AppIdentity.jwtIssuer

    { AccessToken = jwt.AccessToken
      ExpiresIn = jwt.ExpiresIn }

let login (db: DbSession) (jwtConfig: JwtService.JwtConfig) (credentials: Login) =
    db.WithConnection(fun conn ->
        match AuthUserRepository.tryGetByEmail conn credentials.Username with
        | None -> loginFailed
        | Some user ->
            if Auth.validatePassword credentials.Password user.Password then
                AuthUserRepository.updateLastLogin conn user.Id
                LoginSucceeded(tokenResponse user.Id (roleForUser user) jwtConfig.Secret jwtConfig.Audience)
            else
                loginFailed)

let register (db: DbSession) (jwtConfig: JwtService.JwtConfig) (credentials: Login) =
    db.WithConnection(fun conn ->
        match AuthUserRepository.tryGetByEmail conn credentials.Username with
        | Some _ -> registerFailed
        | None ->
            let authUser = createNewUser conn credentials.Username credentials.Password
            RegisterSucceeded(tokenResponse authUser.Id AppIdentity.userRole jwtConfig.Secret jwtConfig.Audience))
