# Applying database migrations

Logbook uses the standalone F# DbUp runner in `api/Migrations/Program.fs`.
Run it against the target PostgreSQL database **before starting the updated API**.
API startup and `docker compose up` do not run migrations. The API reads user and
JWT tables during startup, so an empty database must be migrated first.

## Prerequisites

- Install the .NET 10 SDK on the machine running migrations.
- Create the target PostgreSQL database first and ensure it is reachable.
- Use a database role with permission to create and alter the application tables,
  indexes, functions, and triggers, and to update existing diary rows.
- For an existing deployment, back up the database and first rehearse the upgrade
  on a restored copy. Schedule a maintenance window for large diary datasets and
  pause API writers while migrating. Run only one migration process at a time.

## Apply from the repository root

Supply `DATABASE_URL` through your deployment secrets or local shell environment.
The runner accepts either a PostgreSQL URI or an Npgsql connection string:

```sh
# Example only: replace these values with the target database credentials.
export DATABASE_URL='postgresql://USER:PASSWORD@HOST:5432/DATABASE'
# Alternatively:
# export DATABASE_URL='Host=HOST;Port=5432;Database=DATABASE;Username=USER;Password=PASSWORD'

dotnet restore api/Migrations/Migrations.fsproj
dotnet run --project api/Migrations/Migrations.fsproj --no-restore
```

The runner reads the environment variable directly. A single nonempty argument
overrides it, if needed:

```sh
dotnet run --project api/Migrations/Migrations.fsproj -- "$DATABASE_URL"
```

From `api/`, the existing shortcut is `make migrate`. This passes `DATABASE_URL`
as a command-line argument and Make echoes its command, so prefer the direct
environment-based invocation above when credentials must stay out of logs.
Do not commit credentials to the repository.

The scripts are embedded at build time from `api/Migrations/scripts/*.sql`.
DbUp tracks applied script names in its database journal and runs pending scripts
in name order. Re-running the runner skips scripts already recorded as applied.
It does not offer a dry-run, a target-version selector, or a down-migration command.

## Local Docker Compose database

From the repository root, start just PostgreSQL first:

```sh
docker compose up -d db
docker compose exec db pg_isready -U postgres -d postgres
```

Wait until PostgreSQL accepts connections. Set `DATABASE_URL` using the database
credentials in your local Compose configuration. When running the migration on
the host, use `localhost:5432`; the hostname `db` is for containers on the Compose
network. Apply migrations with the commands above, then start the application:

```sh
docker compose up -d web
```

The Dockerfile in `api/` publishes the API, not the migration runner. Run migrations
from a checkout with the SDK or from a separately published migration artifact;
do not assume the API runtime image contains the migration executable.

## Current migrations

| Script | Effect |
| --- | --- |
| `0001_baseline.sql` | Creates JWT, user, diary, and summary tables and indexes; adds missing diary search columns. |
| `0002_diary_user_note_id_idx.sql` | Ensures the diary index on `(user_id, note_id DESC)` exists. The baseline also includes this index. |
| `0003_todo_table.sql` | Creates the todo table and its user/date index. |
| `0004_diary_sync.sql` | Creates sync state, entries, and receipts; installs the diary trigger; backfills existing diaries. |

Migration 0004 executes `UPDATE diary SET note = note` for diaries without a sync
entry so the trigger records them, including empty notes. Its SQL leaves
`last_updated` unchanged. This can update many rows and hold locks: allow time and
database capacity for the backfill. Apply it before deploying the offline-sync
API/frontend; see [offline sync rollout](offline-sync.md#deployment-and-upgrades).

For a legacy database without a DbUp journal, the runner executes the baseline
as well. Its `IF NOT EXISTS` statements allow compatible existing objects to
remain, but do not validate or repair incompatible column definitions. Test the
full migration sequence against a copy of the actual database first.

## Verify the result

The runner prints `Database migrations completed.` on success and exits with code
0. A reported upgrade failure exits with code 1; missing connection configuration
exits with code 2. Connection-string parsing errors can fail before those messages.
Require a successful exit before deploying the API.

Run these read-only checks in a PostgreSQL client connected to the same database
after migration 0004:

```sql
SELECT to_regclass('public.diary_sync_state') AS sync_state,
       to_regclass('public.diary_sync_entry') AS sync_entry,
       to_regclass('public.diary_sync_receipt') AS sync_receipt;

SELECT tgname, tgenabled
FROM pg_trigger
WHERE tgrelid = 'public.diary'::regclass
  AND tgname = 'diary_sync_changed'
  AND NOT tgisinternal;

SELECT count(*) AS diaries_missing_sync_entries
FROM diary d
WHERE NOT EXISTS (
    SELECT 1 FROM diary_sync_entry s
    WHERE s.user_id = d.user_id AND s.note_id = d.note_id
);
```

With the default `public` schema, expect three non-null table names, one enabled
trigger (`tgenabled = 'O'`), and zero missing sync entries. Adjust schema names if
your deployment uses a different search path. Re-run the migration command to
confirm there are no pending scripts, then start the API and check login, diary
reading, saving, and syncing with a test account.

## If a migration fails

Keep the updated API stopped and inspect the failing script and database state.
The runner does not explicitly configure transaction boundaries; do not assume
the whole upgrade rolled back. Earlier scripts or statements may have persisted.
Fix connection or permission problems, and assess any partial changes before
retrying. Do not delete journal records or mark a failed script as applied simply
to bypass an error. Recover using a reviewed forward migration or the tested
database backup when necessary; there is no automatic rollback command.

## Adding a migration

1. Add the next numbered SQL file, such as `0005_description.sql`, under
   `api/Migrations/scripts/`. Keep applied filenames and contents unchanged: DbUp
   tracks script names, so editing an applied script does not reapply it.
2. Update `api/sql/schema.sql` to match. That file is the schema used by sqlc and
   the integration-test bootstrap; it does not replace the migration history.
3. If query definitions change, regenerate F# queries with `sqlc generate` from
   `api/`, then build the API.
4. Rebuild and run the migration project against both an empty disposable database
   and a copy of the previous schema with representative data. Run it twice to
   check the journal skips applied scripts, and validate the affected app behavior.

The current integration fixture initializes databases through `Database.InitDB.init`
using `schema.sql`, rather than through DbUp. Passing those tests alone does not
verify the migration path. The fixture also truncates application tables: never
point `LOGBOOK_TEST_DATABASE_URL` at a database containing data you need.
