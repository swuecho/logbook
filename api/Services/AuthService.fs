module AuthService

open Database
open Logbook

type Login = { Username: string; Password: string }

type LoginResult =
    | LoginSucceeded of SessionService.IssuedSession
    | LoginFailed of ApiError

type RegisterResult =
    | RegisterSucceeded of SessionService.IssuedSession
    | RegisterFailed of ApiError

let private createNewUser conn email password =
    let passwordHash = Auth.generatePasswordHash password
    AuthUserRepository.create conn email passwordHash "" "" email false false

let private loginFailed = LoginFailed HttpError.invalidCredentials
let private registerFailed = RegisterFailed HttpError.emailAlreadyRegistered

let login (db: DbSession) (jwtConfig: JwtService.JwtConfig) previousToken (credentials: Login) =
    db.WithTransaction(fun conn ->
        match AuthUserRepository.tryGetByEmail conn credentials.Username with
        | None -> loginFailed
        | Some user ->
            if user.IsActive && not (isNull credentials.Password) && Auth.validatePassword credentials.Password user.Password then
                AuthUserRepository.updateLastLogin conn user.Id
                LoginSucceeded(SessionService.issue conn jwtConfig user.Id user.IsSuperuser previousToken)
            else
                loginFailed)

let register (db: DbSession) (jwtConfig: JwtService.JwtConfig) previousToken (credentials: Login) =
    db.WithTransaction(fun conn ->
        match AuthUserRepository.tryGetByEmail conn credentials.Username with
        | Some _ -> registerFailed
        | None ->
            let authUser = createNewUser conn credentials.Username credentials.Password
            RegisterSucceeded(SessionService.issue conn jwtConfig authUser.Id false previousToken))
