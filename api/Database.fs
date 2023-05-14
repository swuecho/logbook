namespace Database

open System
open Npgsql.FSharp
open FSharp.Reflection

module Config =
    /// Custom operator for combining paths
    let postgresDSN =
        try
            System.Environment.GetEnvironmentVariable("DATABASE_URL")
        with _ ->
            raise (System.Exception "check if BESTQA_FS_PORT is set")

    let connection () = postgresDSN |> Sql.connect

    let conn () =
        new Npgsql.NpgsqlConnection(postgresDSN)

