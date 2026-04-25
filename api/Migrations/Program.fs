open System
open System.Reflection
open DbUp

let private tryGetConnectionString (args: string array) =
    match args with
    | [| connectionString |] when String.IsNullOrWhiteSpace(connectionString) |> not ->
        Some connectionString
    | _ ->
        let value = Environment.GetEnvironmentVariable("DATABASE_URL")

        if String.IsNullOrWhiteSpace(value) then
            None
        else
            Some value

let private normalizeConnectionString (connectionString: string) =
    if connectionString.Contains("://") then
        Npgsql.FSharp.Sql.fromUri (Uri connectionString)
    else
        connectionString

let connectionString =
    match tryGetConnectionString (Environment.GetCommandLineArgs() |> Array.skip 1) with
    | Some value -> normalizeConnectionString value
    | None ->
        eprintfn "Usage: dotnet run --project Migrations -- <connection-string>"
        eprintfn "Or set DATABASE_URL."
        exit 2

let upgrader =
    DeployChanges
        .To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build()

let result = upgrader.PerformUpgrade()

let exitCode =
    if result.Successful then
        printfn "Database migrations completed."
        0
    else
        eprintfn "Database migration failed: %O" result.Error
        1

Environment.Exit(exitCode)
