namespace Database

open System
open Microsoft.AspNetCore.Http

module Config =
    /// Custom operator for combining paths
    let postgresDSN =
        try
            System.Environment.GetEnvironmentVariable("DATABASE_URL")
        with _ ->
            raise (System.Exception "check if BESTQA_FS_PORT is set")
    let connStr = 
        let pgConnStr = Npgsql.FSharp.Sql.fromUri (Uri postgresDSN)
        //https://stackoverflow.com/questions/40364449/npgsql-exception-while-reading-from-stream-postgres
        // wait longger
        pgConnStr + ";Timeout=300;CommandTimeout=300"


module Connection =
    open Npgsql
    let getConn (httpContext: HttpContext) =
        match httpContext.Items.["NpgsqlConnection"] with
            | :? NpgsqlConnection as connection -> connection
            | _ -> failwith "can not get connection"


    let UseNpgsqlConnectionMiddleware (connectionString: string) (context: HttpContext) (next: RequestDelegate) =

        let openConn () =
            let connection = new NpgsqlConnection(connectionString)
            context.Items.["NpgsqlConnection"] <- connection
            connection.Open()

        let closeConn () =
            match context.Items.["NpgsqlConnection"] with
            | :? NpgsqlConnection as connection -> connection.Close() //; connection.Dispose()
            | _ -> ()

        let cleanup =
            { new System.IDisposable with
                member this.Dispose() = closeConn () }

        try
            openConn ()
            next.Invoke context
        finally
            cleanup.Dispose()
