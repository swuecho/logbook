module AuthService

open System.Net
open Npgsql

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

let private issuer = "logbook-swuecho.github.com"
let private jwtAudienceName = "logbook"

let createNewUser (conn: NpgsqlConnection) email password =
    let passwordHash = Auth.generatePasswordHash password

    let newUser: AuthUser.CreateAuthUserParams =
        { Email = email
          Password = passwordHash
          FirstName = ""
          LastName = ""
          Username = email
          IsStaff = false
          IsSuperuser = false }

    AuthUser.CreateAuthUser conn newUser

let private tokenResponse userId role jwtKey audience =
    let jwt = Token.generateToken userId role jwtKey audience issuer

    { AccessToken = jwt.AccessToken
      ExpiresIn = jwt.ExpiresIn }

let login (conn: NpgsqlConnection) (credentials: Login) =
    let email = credentials.Username
    let secret = JwtSecrets.GetJwtSecret conn jwtAudienceName
    let jwtKey = secret.Secret
    let audience = secret.Audience

    if AuthUser.CheckUserExists conn email then
        let user = AuthUser.GetUserByEmail conn email
        let passwordMatches = Auth.validatePassword credentials.Password user.Password
        let role = if user.IsSuperuser then "admin" else "user"

        if passwordMatches then
            AuthUser.UpdateLastLogin conn user.Id |> ignore
            LoginSucceeded(tokenResponse user.Id role jwtKey audience)
        else
            LoginFailed
                { Code = HttpStatusCode.Unauthorized
                  Message = "Login failed. password or email is wrong" }
    else
        let authUser = createNewUser conn email credentials.Password
        LoginSucceeded(tokenResponse authUser.Id "user" jwtKey audience)
