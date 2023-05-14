namespace Database

open System
open Npgsql.FSharp

module Config =
    /// Custom operator for combining paths
    let postgresDSN =
        try
            System.Environment.GetEnvironmentVariable("DATABASE_URL")
        with _ ->
            raise (System.Exception "check if BESTQA_FS_PORT is set")

    let conn () =
        let connStr = Npgsql.FSharp.Sql.fromUri (Uri postgresDSN)
        new Npgsql.NpgsqlConnection(connStr)
