module JwtService

type JwtConfig =
    { Secret: string
      Audience: string }


let getOrCreateJwtSecret pgConn jwtAudienceName =
    let getExistingSecret () =
        match JwtSecretRepository.tryGetByName pgConn jwtAudienceName with
        | Some secret ->
            printfn "Existing JWT Secret found for %s" jwtAudienceName
            Some secret
        | None -> None

    let getJwtKey () =
        Util.requiredEnvVar "JWT_SECRET"

    let getAudience () =
        Util.requiredEnvVar "JWT_AUDIENCE"

    let createNewSecret () =
        let createdSecret = JwtSecretRepository.create pgConn jwtAudienceName (getJwtKey ()) (getAudience ())
        printfn "New JWT Secret created for %s" jwtAudienceName
        createdSecret

    match getExistingSecret () with
    | Some secret -> secret
    | None -> createNewSecret ()
