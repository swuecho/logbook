module Token

open System
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens

let generateToken (userId: int) (role: string) (secret: string) (audience: string) (issuer: string) =
    let expires = Nullable(DateTime.UtcNow.AddHours(168.0))
    let notBefore = Nullable(DateTime.UtcNow)

    let securityKey =
        secret |> System.Text.Encoding.UTF8.GetBytes |> SymmetricSecurityKey

    let signingCredentials =
        SigningCredentials(key = securityKey, algorithm = SecurityAlgorithms.HmacSha256)

    let roleClaim = Claim(AppIdentity.roleClaim, role)
    let userIdClaim = Claim(AppIdentity.userIdClaim, userId |> string)

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
       ExpiresIn = 604800 |}
