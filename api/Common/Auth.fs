module Auth

open System.Security.Cryptography

let iterations = 260000
let saltSize = 16
let keySize = 32
let hashAlgorithm = HashAlgorithmName.SHA256
let passwordHashScheme = "pbkdf2_sha256"

let generateSalt () =
    let salt = Array.zeroCreate saltSize
    let rng = RandomNumberGenerator.Create()
    rng.GetBytes(salt)
    salt

let private derivePasswordHashBytes (password: string) (salt: byte array) =
    Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hashAlgorithm, keySize)

let generatePasswordHashWithSalt (password: string) (salt: byte array) =
    let hashBytes = derivePasswordHashBytes password salt
    let encodedHash = System.Convert.ToBase64String hashBytes
    let encodedSalt = System.Convert.ToBase64String salt
    $"{passwordHashScheme}${iterations}${encodedSalt}${encodedHash}"

let generatePasswordHash (password: string) =
    generatePasswordHashWithSalt password (generateSalt ())

let validatePassword (password: string) (hash: string) =
    match hash.Split('$') with
    | [| scheme; n; encodedSalt; encodedHash |] when scheme = passwordHashScheme && int n = iterations ->
        let salt = System.Convert.FromBase64String encodedSalt
        let decodedHash = System.Convert.FromBase64String encodedHash

        let computedHash = derivePasswordHashBytes password salt

        if decodedHash = computedHash then true else false
    | _ -> false

let generateRandomPassword () =
    let letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"
    let password = Array.zeroCreate 12
    let rng = RandomNumberGenerator.Create()
    rng.GetBytes password

    password
    |> Array.map (fun x -> letters.[int (x) % letters.Length])
    |> Seq.toArray
    |> System.String
