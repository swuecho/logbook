namespace Database

open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Npgsql

type DbSession(dataSource: NpgsqlDataSource) =
    member _.WithConnection(action: NpgsqlConnection -> 'T) : 'T =
        use conn = dataSource.OpenConnection()
        action conn

    member _.WithTransaction(action: NpgsqlConnection -> 'T) : 'T =
        use conn = dataSource.OpenConnection()
        use transaction = conn.BeginTransaction()

        try
            let result = action conn
            transaction.Commit()
            result
        with _ ->
            transaction.Rollback()
            reraise ()


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
    let init (dataSource: NpgsqlDataSource) =
        use conn = dataSource.OpenConnection()
        // run the init script from file api/sql/schema.sql
        let initScript = System.IO.File.ReadAllText(__SOURCE_DIRECTORY__ + "/../sql/schema.sql")
        use cmd = new NpgsqlCommand(initScript, conn)
        cmd.ExecuteNonQuery()  |> printfn "schema.sql executed %d"


module Connection =
    let createDataSource (connectionString: string) : NpgsqlDataSource =
        NpgsqlDataSource.Create(connectionString)

    let addDatabase (dataSource: NpgsqlDataSource) (services: IServiceCollection) =
        services.AddSingleton<NpgsqlDataSource>(dataSource) |> ignore
        services.AddSingleton<DbSession>(fun _ -> DbSession(dataSource)) |> ignore
        services

    let dbSession (httpContext: HttpContext) =
        httpContext.RequestServices.GetRequiredService<DbSession>()
