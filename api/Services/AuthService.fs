module AuthService

open System.Net
open Database

type Login = { Username: string; Password: string }

type AccessTokenResponse =
    { AccessToken: string
      ExpiresIn: int }

type LoginFailure =
    { Code: HttpStatusCode
      Message: string }

type LoginResult =
    | LoginSucceeded of AccessTokenResponse
    | LoginFailed of LoginFailure

let private createNewUser conn email password =
    let passwordHash = Auth.generatePasswordHash password
    AuthUserRepository.create conn email passwordHash "" "" email false false

let private loginFailed =
    LoginFailed
        { Code = HttpStatusCode.Unauthorized
          Message = "Login failed. password or email is wrong" }

let private roleForUser user =
    if user.IsSuperuser then
        AppIdentity.adminRole
    else
        AppIdentity.userRole

let private tokenResponse userId role jwtKey audience =
    let jwt = Token.generateToken userId role jwtKey audience AppIdentity.jwtIssuer

    { AccessToken = jwt.AccessToken
      ExpiresIn = jwt.ExpiresIn }

let private loginExistingUser conn credentials jwtKey audience =
    let user = AuthUserRepository.getByEmail conn credentials.Username

    if Auth.validatePassword credentials.Password user.Password then
        AuthUserRepository.updateLastLogin conn user.Id
        LoginSucceeded(tokenResponse user.Id (roleForUser user) jwtKey audience)
    else
        loginFailed

let private createAndLoginUser conn credentials jwtKey audience =
    let authUser = createNewUser conn credentials.Username credentials.Password
    LoginSucceeded(tokenResponse authUser.Id AppIdentity.userRole jwtKey audience)

let login (db: DbSession) (credentials: Login) =
    db.WithConnection(fun conn ->
        let email = credentials.Username
        let secret = JwtSecretRepository.getByName conn AppIdentity.jwtAudienceName
        let jwtKey = secret.Secret
        let audience = secret.Audience

        if AuthUserRepository.existsByEmail conn email then
            loginExistingUser conn credentials jwtKey audience
        else
            createAndLoginUser conn credentials jwtKey audience)
