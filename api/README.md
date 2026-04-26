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

## Database: DbUp vs `sql/schema.sql`

**Applying schema changes (real databases)**  
Use **DbUp** from the `Migrations` project. Add new versioned SQL under `Migrations/scripts/`, then run:

```sh
make migrate
```

`make migrate` uses `DATABASE_URL`, or you can pass a connection string directly:

```sh
dotnet run --project Migrations -- "$DATABASE_URL"
```

**sqlc and type-checked queries**  
`sql/schema.sql` is the **sqlc** schema: it drives generated F# in `queries/` and validation of `sql/query/*.sql`. When you add tables or columns, update **both** a DbUp migration and `sql/schema.sql` (so `sqlc generate` and `dotnet build` match the real database).

**Optional local bootstrap**  
Set `LOGBOOK_RUN_SCHEMA_INIT_ON_STARTUP=true` to run the full `schema.sql` against an empty database (e.g. quick local setup). This does not replace migrations for shared or production databases.

**Search index (optional on startup)**  
`LOGBOOK_REFRESH_SEARCH_INDEX_ON_STARTUP=true` refreshes missing search index rows; not enabled by default.

## Port

in prod, default port is 8080 (aspnet 8), 80 (aspnet < 8).
in dev, default port is 500

## API client errors

Failed requests that return a JSON body use a single shape: `{ "code": string, "message": string }`. The HTTP status code carries the outcome (e.g. `401` / `403`); `code` is a short machine-readable label such as `unauthorized` or `invalid_credentials`.

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
