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
        use conn = new NpgsqlConnection(connectionString)
        conn.Open()
        // run the init script from file api/sql/schema.sql
        let initScript = System.IO.File.ReadAllText( __SOURCE_DIRECTORY__ + "/sql/schema.sql")
        use cmd = new NpgsqlCommand(initScript, conn)
        cmd.ExecuteNonQuery()  |> printfn "schema.sql executed %d"


module Connection =
    open Npgsql

    let private connectionItemKey = "NpgsqlConnection"

    type HttpContext with
        member this.GetNpgsqlConnection() =
            match this.Items.[connectionItemKey] with
            | :? NpgsqlConnection as connection -> connection
            | _ -> failwith "can not get connection"

    let getConn (httpContext: HttpContext) =
        httpContext.GetNpgsqlConnection()


    let UseNpgsqlConnectionMiddleware (connectionString: string) (context: HttpContext) (next: RequestDelegate) =
        task {
            use connection = new NpgsqlConnection(connectionString)
            context.Items.[connectionItemKey] <- connection
            connection.Open()
            try
                return! next.Invoke context
            finally
                context.Items.Remove(connectionItemKey) |> ignore
        }
