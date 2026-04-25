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

let private tokenResponse userId role jwtKey audience =
    let jwt = Token.generateToken userId role jwtKey audience AppIdentity.jwtIssuer

    { AccessToken = jwt.AccessToken
      ExpiresIn = jwt.ExpiresIn }

let login (db: DbSession) (credentials: Login) =
    db.WithConnection(fun conn ->
        let email = credentials.Username
        let secret = JwtSecretRepository.getByName conn AppIdentity.jwtAudienceName
        let jwtKey = secret.Secret
        let audience = secret.Audience

        if AuthUserRepository.existsByEmail conn email then
            let user = AuthUserRepository.getByEmail conn email
            let passwordMatches = Auth.validatePassword credentials.Password user.Password
            let role =
                if user.IsSuperuser then
                    AppIdentity.adminRole
                else
                    AppIdentity.userRole

            if passwordMatches then
                AuthUserRepository.updateLastLogin conn user.Id
                LoginSucceeded(tokenResponse user.Id role jwtKey audience)
            else
                LoginFailed
                    { Code = HttpStatusCode.Unauthorized
                      Message = "Login failed. password or email is wrong" }
        else
            let authUser = createNewUser conn email credentials.Password
            LoginSucceeded(tokenResponse authUser.Id AppIdentity.userRole jwtKey audience))
