namespace Database

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Npgsql

type DbSession(dataSource: NpgsqlDataSource) =
    let isDisposedConnectorState (ex: exn) =
        match ex with
        | :? ObjectDisposedException as disposed ->
            disposed.ObjectName = "System.Threading.ManualResetEventSlim"
        | _ -> false

    member _.WithConnection(action: NpgsqlConnection -> 'T) : 'T =
        let execute () =
            use conn = dataSource.OpenConnection()
            action conn

        try
            execute ()
        with ex when isDisposedConnectorState ex ->
            NpgsqlConnection.ClearAllPools()
            execute ()

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
    let postgresDSN =
        Util.requiredEnvVar "DATABASE_URL"

    let connStr =
        let pgConnStr = Npgsql.FSharp.Sql.fromUri (System.Uri postgresDSN)
        //https://stackoverflow.com/questions/40364449/npgsql-exception-while-reading-from-stream-postgres
        // wait longger
        pgConnStr + ";Timeout=300;CommandTimeout=300"

module InitDB =
    let init (dataSource: NpgsqlDataSource) =
        use conn = dataSource.OpenConnection()
        // For local one-off only: same file sqlc uses; production DBs should use DbUp (see api/README).
        let initScript = System.IO.File.ReadAllText(__SOURCE_DIRECTORY__ + "/../sql/schema.sql")
        use cmd = new NpgsqlCommand(initScript, conn)
        cmd.ExecuteNonQuery() |> printfn "schema.sql executed %d"


module Connection =
    let createDataSource (connectionString: string) : NpgsqlDataSource =
        NpgsqlDataSource.Create(connectionString)

    let addDatabase (dataSource: NpgsqlDataSource) (services: IServiceCollection) =
        services.AddSingleton<NpgsqlDataSource>(dataSource) |> ignore
        services.AddSingleton<DbSession>(fun _ -> DbSession(dataSource)) |> ignore
        services

    let dbSession (httpContext: HttpContext) =
        httpContext.RequestServices.GetRequiredService<DbSession>()
