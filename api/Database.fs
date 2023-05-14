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

    let connStr = Npgsql.FSharp.Sql.fromUri (Uri postgresDSN)
    let conn () = new Npgsql.NpgsqlConnection(connStr)


module Connection =
    open Microsoft.AspNetCore.Http
    open Npgsql

    let UseNpgsqlConnectionMiddleware (connectionString: string) (next: RequestDelegate) (context: HttpContext) =

        let openConn (httpContext: HttpContext) (next: RequestDelegate) =
            let connection = new NpgsqlConnection(connectionString)
            httpContext.Items.["NpgsqlConnection"] <- connection
            connection.Open()
            next.Invoke httpContext

        let closeConn (httpContext: HttpContext) =
            match httpContext.Items.["NpgsqlConnection"] with
            | :? NpgsqlConnection as connection -> connection.Close()
            | _ -> ()

        let cleanup =
            { new System.IDisposable with
                member this.Dispose() = closeConn context }

        try
            openConn context next
        finally
            cleanup.Dispose()
