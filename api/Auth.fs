module Auth

open System.Security.Cryptography

let iterations = 260000
let saltSize = 16
let keySize = 32

let generateSalt () =
    let salt = Array.zeroCreate saltSize
    let rng = RandomNumberGenerator.Create()
    rng.GetBytes(salt)
    salt

let generatePasswordHash (password: string) =
    let salt = generateSalt ()

    let derivBytes =
        new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)

    let hashBytes = derivBytes.GetBytes keySize
    let encodedHash = System.Convert.ToBase64String hashBytes
    let encodedSalt = System.Convert.ToBase64String salt
    $"pbkdf2_sha256${iterations}${encodedSalt}${encodedHash}"

let validatePassword (password: string) (hash: string) =
    match hash.Split('$') with
    | [| "pbkdf2_sha256"; n; encodedSalt; encodedHash |] when int n = iterations ->
        let salt = System.Convert.FromBase64String encodedSalt
        let decodedHash = System.Convert.FromBase64String encodedHash

        let derivBytes =
            new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)

        let computedHash = derivBytes.GetBytes keySize
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


