# F# Project

## Project Structure

*.fsproj: This is your project file, defining project settings, dependencies, and build configurations.

Program.fs: This is the main entry point of your F# ASP.NET Core application

## Develop

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

## Regenerating F# SQL Queries

The backend uses [`sqlc`](https://sqlc.dev/) with the `sqlc-fs` plugin to generate F# query modules.

Source files:

- SQL schema: `sql/schema.sql`
- SQL queries: `sql/query/`
- Generated F# output: `queries/`
- sqlc config: `sqlc.json`

Install the generator tools:

```sh
# Install sqlc if needed.
brew install sqlc

# Install the published sqlc-fs plugin.
go install github.com/swuecho/sqlc-fs@latest
```

If you are working on local `sqlc-fs` fixes, install from the local checkout instead. `@latest` only uses the latest tagged module release, so it may not include unpublished fixes.

```sh
cd /Users/hwu/dev/sqlc-fs
go install .
```

Regenerate and verify:

```sh
sqlc generate
dotnet build
```

When adding query parameters that sqlc cannot name well, prefer named args in SQL:

```sql
sqlc.arg(search_terms)
sqlc.arg(separator)
```

This avoids generated names like `Column2` and keeps the F# parameter records readable.
