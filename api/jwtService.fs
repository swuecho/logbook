module JwtService 

type JwtConfig = 
    { Secret: string
      Audience: string }

let getOrCreateJwtSecret pgConn jwtAudienceName =
    let getExistingSecret () =
        try
            let secret = JwtSecrets.GetJwtSecret pgConn jwtAudienceName
            printfn "Existing JWT Secret found for %s" jwtAudienceName
            Some secret
        with :? NoResultsException ->
            None

    let generateRandomKey () =
        System.Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))

    let getJwtKey () =
        match Util.getEnvVar "JWT_SECRET" with
        | null ->
            let randomKey = generateRandomKey ()
            printfn "Warning: JWT_SECRET not set. Using randomly generated key: %s" randomKey
            randomKey
        | key -> key

    let getAudience () =
        match Util.getEnvVar "JWT_AUDIENCE" with
        | null ->
            let defaultAudience = generateRandomKey ()
            printfn "Warning: JWT_AUDIENCE not set. Using default audience: %s" defaultAudience
            defaultAudience
        | aud -> aud

    let createNewSecret () =
        let jwtSecretParams: JwtSecrets.CreateJwtSecretParams =
            { Name = jwtAudienceName
              Secret = getJwtKey ()
              Audience = getAudience () }

        let createdSecret = JwtSecrets.CreateJwtSecret pgConn jwtSecretParams
        printfn "New JWT Secret created for %s" jwtAudienceName
        createdSecret

    match getExistingSecret () with
    | Some secret -> secret
    | None -> createNewSecret ()
