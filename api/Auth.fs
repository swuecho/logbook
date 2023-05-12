module Auth

open System
open Microsoft.AspNetCore.Http
open Microsoft.IdentityModel.Tokens
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt

type JwtToken =
    { UserId: int
      Role: string
      Secret: string
      Audience: string
      Expires: int64
      IssuedAt: int64 }

let generateJwtSecretAndAudience () =
    let secretBytes = Array.init 32 (fun _ -> uint8 (Random().Next(256)))
    let secret = secretBytes |> Convert.ToBase64String

    let letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    let audienceBytes = Array.init 32 (fun _ -> letters.[Random().Next(letters.Length)])
    let audience = new String(audienceBytes)
    secret, audience


let generateToken (userId: int) (role: string) (secret: string) (audience: string) (issuer: string) =
    let expires = Nullable(DateTime.UtcNow.AddHours(8.0))
    let notBefore = Nullable(DateTime.UtcNow)

    let securityKey =
        secret |> System.Text.Encoding.UTF8.GetBytes |> SymmetricSecurityKey

    let signingCredentials =
        SigningCredentials(key = securityKey, algorithm = SecurityAlgorithms.HmacSha256)

    let roleClaim = Claim("role", role)
    let userIdClaim = Claim("user_id", userId |> string)

    let token =
        JwtSecurityToken(
            issuer = issuer,
            audience = audience,
            claims = [ roleClaim; userIdClaim ],
            expires = expires,
            notBefore = notBefore,
            signingCredentials = signingCredentials
        )

    {| AccessToken = JwtSecurityTokenHandler().WriteToken(token)
       ExpiresIn = 60480 |}


let validateJwt (token: string) (secretKey: string) =

    let decodeJwt (key: string) (token: string) =
        let jwt = JwtSecurityTokenHandler()
        let keyBytes = System.Text.Encoding.UTF8.GetBytes(key)
        let validationParameters = TokenValidationParameters()
        //validationParameters.ValidateIssuer <- true
        //validationParameters.ValidIssuer <- "myIssuer"
        //validationParameters.ValidateAudience <- true
        // validationParameters.ValidAudience <- "myAudience"
        validationParameters.ValidateLifetime <- true
        validationParameters.ValidateIssuerSigningKey <- true
        validationParameters.IssuerSigningKey <- new SymmetricSecurityKey(keyBytes)
        jwt.ValidateToken(token, validationParameters)

    try
        decodeJwt secretKey token |> ignore
        true
    with :? SecurityTokenValidationException ->
        false

let getJwtClaim (token: string) (claimType: string) =
    let jwt = JwtSecurityTokenHandler()
    let token = jwt.ReadJwtToken(token)
    let claims = token.Claims
    claims |> Seq.find (fun c -> c.Type = claimType)


let getExpireSecureCookie (value: string, isHttps: bool) =
    let utcOffset = DateTimeOffset.UtcNow.AddDays(-1)

    {| Name = "jwt"
       Value = value
       Path = "/"
       HttpOnly = true
       Secure = isHttps
       SameSite = SameSiteMode.Strict
       Expires = utcOffset |}
