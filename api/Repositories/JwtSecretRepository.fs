module JwtSecretRepository

open Npgsql

let getByName (conn: NpgsqlConnection) name =
    JwtSecrets.GetJwtSecret conn name

let tryGetByName (conn: NpgsqlConnection) name =
    try
        Some(JwtSecrets.GetJwtSecret conn name)
    with :? NoResultsException ->
        None

let create (conn: NpgsqlConnection) name secret audience =
    JwtSecrets.CreateJwtSecret
        conn
        { Name = name
          Secret = secret
          Audience = audience }
