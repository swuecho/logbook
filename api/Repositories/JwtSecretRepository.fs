module JwtSecretRepository

open Npgsql

let getByName (conn: NpgsqlConnection) name =
    JwtSecrets.GetJwtSecret conn name

let create (conn: NpgsqlConnection) name secret audience =
    JwtSecrets.CreateJwtSecret
        conn
        { Name = name
          Secret = secret
          Audience = audience }
