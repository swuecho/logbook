namespace Database

open Microsoft.AspNetCore.Http


module Config =
    /// Custom operator for combining paths
    let postgresDSN =
        try
            System.Environment.GetEnvironmentVariable("DATABASE_URL")
        with _ ->
            raise (System.Exception "check if DATABASE_URL is set")
    let connStr = 
        let pgConnStr = Npgsql.FSharp.Sql.fromUri (System.Uri postgresDSN)
        //https://stackoverflow.com/questions/40364449/npgsql-exception-while-reading-from-stream-postgres
        // wait longger
        printfn "pgConnStr: %s" pgConnStr
        pgConnStr + ";Timeout=300;CommandTimeout=300"
    
module InitDB =
    open Npgsql
    let init (connectionString: string) =
        let conn = new NpgsqlConnection(connectionString)
        conn.Open()
        // run the init script from file api/sql/schema.sql
        let initScript = System.IO.File.ReadAllText( __SOURCE_DIRECTORY__ + "/sql/schema.sql")
        let cmd = new NpgsqlCommand(initScript, conn)
        cmd.ExecuteNonQuery()  |> printfn "schema.sql executed %d"
        conn.Close()


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
