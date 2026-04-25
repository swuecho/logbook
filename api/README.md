# F# project




## project structure

*.fsproj: This is your project file, defining project settings, dependencies, and build configurations.

Program.fs: This is the main entry point of your F# ASP.NET Core application

## develop

```sh
dotnet restore
dotnet build
dotnet watch run
```

Run database migrations before starting the API:

```sh
make migrate
```

`make migrate` uses `DATABASE_URL`, or you can pass a connection string directly:

```sh
dotnet run --project Migrations -- "$DATABASE_URL"
```

The API no longer initializes the schema or refreshes missing search indexes on every startup. For local one-off fallback behavior, set `LOGBOOK_RUN_SCHEMA_INIT_ON_STARTUP=true` or `LOGBOOK_REFRESH_SEARCH_INDEX_ON_STARTUP=true` before running the web app.

## Port

in prod, default port is 8080 (aspnet 8), 80 (aspnet < 8).
in dev, default port is 500

## sqlc

```
sqlc generate
```