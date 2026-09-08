# Offline diary storage and sync

The editor reads and saves in IndexedDB. Network requests run separately, so a
slow or unavailable server does not block cached entries or writing. This release
implements roadmap phases 1–3. Calendar dates come from local entries; search,
word-cloud summaries, aggregated todos, and Markdown export still require the
server (phase 4). External image and iframe content is not downloaded for offline
use, although the document references are preserved.

## User behavior

1. Sign in online once. The app downloads the currently opened entry, then history
   in resumable batches. Open **Details** in the sync bar to check that both the app
   and history are downloaded before going offline.
2. Write normally. **Saved on this device** means IndexedDB committed the edit.
   **Synced** means the server acknowledged it. Local storage failure has a separate
   error, retry, and download action; route navigation waits for local writes.
3. Reopening a production build offline works after the service worker has
   installed. Expired server credentials do not lock the local diary. Sign in to
   the same account when available to resume uploading.
4. When two devices edit the same date, the editor keeps the local version and
   displays the server version for review. Both versions are retained in the
   device backup after choosing either one.
5. **Export device backup** includes pending entries and conflict recovery copies.
   **Keep offline storage** requests persistent browser storage, which the browser
   may decline. Clearing site data removes local writing. Logout clears the login
   immediately, even offline, but retains each account's separate local partition.

Sync runs on startup, reconnect, focus, an explicit retry, and periodically while
the app is open. It uses 12-second request timeouts and bounded exponential
backoff. There is no promise of background upload after the browser closes.

## Local data and concurrency

Database `logbook-db`, version 2:

- `entries`: primary key `[account, noteId]`, with an account index. The account
  includes origin, token issuer/audience, and user ID; a date is `YYYYMMDD` in the
  user's calendar, never converted into a UTC timestamp.
- `syncMeta`: per-account download cursor and initial-history completion state.

Legacy date-only caches are no longer read, migrated, or exported. Existing
unused stores are ignored; new databases contain only the stores above.
If an older tab blocks the database upgrade, the sync details ask the user to close it.

Each edit commits content and dirty state in one transaction. A persisted pending
mutation contains its UUID, base server revision, document, and local edit counter.
Retries resend that same mutation even if typing continues. Acknowledgement clears
only the uploaded edit counter, preserving newer writing. Downloads observed during
an upload are retained and reconciled afterward. An edit based on content the editor
hasn't yet displayed is treated as a conflict, rather than adopting an unseen
server revision. Cross-tab sync uses Web Locks where available; mutation receipts
make duplicate uploads safe when locks are unavailable.

## Server protocol

All endpoints require the existing bearer authentication and validate dates.
Revisions and cursors are decimal strings to avoid JavaScript integer rounding.

| Endpoint | Request | Response |
| --- | --- | --- |
| `GET /api/sync/diary/{date}` | — | `{ noteId, note, revision }`; absent date has revision `"0"`, with no write |
| `PUT /api/sync/diary/{date}` | `{ note, baseRevision, mutationId }` | Saved entry; `409 { current: entry }` if the base changed |
| `GET /api/sync/changes?cursor=0` | Last committed local cursor | `{ entries, cursor, hasMore }`, at most 100 latest entries per page |

A per-user counter is held until the write transaction commits. This prevents a
reader from advancing past an uncommitted smaller revision. Each entry's latest
content and revision are captured by a diary trigger, including changes from the
legacy save endpoint. Empty content remains a versioned entry, so clearing a day
propagates. Hard deletion of individual diary rows is not a supported sync operation;
any future delete endpoint must add versioned deletion markers.

The save transaction atomically checks the base revision, writes the diary, and
records the mutation receipt. Receipts store a content hash, the normalization
result, and revision, rather than another full copy of every document. Replaying a
committed request returns its original acknowledgement without reverting later
writes. Reusing its UUID for a different request is rejected. Receipts should not
be pruned until a protocol for expiring old pending client mutations exists.

A page and its cursor commit in one local transaction. The feed contains current
entry states, not an audit log; this is sufficient to converge diary replicas.

## Deployment and upgrades

1. Back up the server database. Let old clients finish syncing before upgrading
   where possible, and preserve/export any old cached drafts.
2. Run `make migrate` from `api/` using the deployment's `DATABASE_URL`. Migration
   `0004_diary_sync.sql` adds the sync tables, trigger, and backfills existing diaries.
   Apply it before starting the updated backend. The backfill updates existing rows
   to fire the trigger, so schedule it appropriately for database size.
3. Build the frontend with Node 20+ and `yarn install --frozen-lockfile`, then
   `yarn build` from `web/`. Ship the resulting `api/wwwroot` with the API. The build
   generates `sw.js` from only the current compilation's assets, including lazy
   routes. Serve the site over HTTPS (localhost works for development).
4. Avoid long-lived intermediary caching of `sw.js` and `index.html`. The worker
   caches only the app assets, never authenticated API responses. New workers wait
   until older tabs close rather than forcing a reload during writing.
5. Reopen clients online. Confirm **App ready to reopen offline** and **History
   downloaded** in Details.

The legacy PUT endpoint remains compatible for existing clients and still has
unconditional replacement semantics; revision conflict protection is provided by
the new sync endpoint. Upgrade all clients to receive it. Do not roll back the
frontend to v1 while v2 has pending entries: the old client cannot read the new
account-scoped store. A service worker update also requires closing old tabs.

Offline session access is a local convenience, not new server authorization.
Revoked or expired tokens are still rejected by the API. Local caches are not
application-encrypted, and logging out does not erase them.

## Validation

From the repository root:

```sh
dotnet test api/tests/unit.fsproj
```

From `web/`:

```sh
yarn test
yarn build
yarn playwright install chromium
yarn test:e2e
```

For an existing test browser, set `PLAYWRIGHT_CHROMIUM_EXECUTABLE` to its executable
path. The browser tests start an isolated static server on port 9197 and mock the
API; backend integration tests independently exercise PostgreSQL and the real
HTTP handlers using the existing test fixture.

Coverage includes stale acknowledgements, crash-safe mutation retries, unseen and
out-of-order downloads, account isolation, conflicts, empty entries,
concurrent server creation, receipt replay, history pagination,
offline reload with expired credentials, cached calendar navigation, server failure,
and local storage errors. Browser screenshots are written to `web/test-results/`.

### Upload failures

A failed request for one date leaves its pending mutation intact and lets other
uploads and history downloads continue. Details lists the failed dates with links
back to their entries. Retries retain the original mutation ID, including after a
reload, because the server may have committed a request whose response was lost.
The existing bounded backoff applies when any upload fails; **Sync now** retries
immediately. A successful acknowledgement clears that date's failure indicator.
Authentication failures (401/403) and local storage errors stop the current run.

### Tests with the real API and migrations

From `web/`, with Docker running, the .NET 10 SDK, Node, and dependencies installed:

```sh
npm run build
npx playwright install chromium
npm run test:e2e:real
```

This suite creates a disposable PostgreSQL 16 container on a randomly assigned
local port, applies DbUp migrations, runs them again to check the journal is
unchanged, and starts the real API on port 9197. It generates its own database and
JWT credentials and does not use your deployment's `DATABASE_URL`. The container
and its volumes are removed when the server exits normally or receives a stop
signal. Keep port 9197 free and run this separately from `test:e2e`.

Two independent browser contexts sign into the same test account. Tests cover
offline editing and reload, conflicting edits, conflict recovery, a committed
upload with a lost response and retry after reload, entry-specific rejection with
other uploads and downloads continuing, and account-wide authentication failures.
Only selected failures are injected at the browser network boundary; successful
requests go through the real API and migrated PostgreSQL database.

`PLAYWRIGHT_CHROMIUM_EXECUTABLE` can select an existing browser executable. The
failed-upload UI screenshot is written to `web/test-results/sync-upload-failure.png`.
The existing `npm run test:e2e` suite continues to use its lightweight mocked API.

### Reviewing conflicting versions

The conflict panel compares **Your writing** and the **Server version** side by
side (stacked on narrow screens). These are text previews; images and embedded
content appear as descriptions without downloading media. The editor retains the
rich document, including formatting and media references.

Choose **Keep my writing** or **Use server version** to resolve directly, or choose
**Edit combined version** to place both documents, local first, into the editor.
This is a starting draft: review and remove repeated passages yourself. Both
originals are saved in device-backup recovery history before the draft is created.

Combined drafts autosave locally and survive navigation or an offline reload.
They remain blocked from upload until you choose **Use combined version**.
**Back to my original** restores the original local writing and keeps the conflict
open; the edited combined draft is also retained in recovery history. Choosing
**Use server version** retains the combined draft in recovery before resolving.

If the server changes again during review, its latest version appears in the
comparison with a notice; your draft remains intact. Resolution checks the
reviewed content and server revision within the local transaction, and normal
server revision checks still apply when the confirmed draft uploads. Conflict
actions are disabled in a read-only tab or while local storage has a save error.

The real-API suite also checks combined editing, offline reload, newer server
changes during review, explicit confirmation, and both-device convergence. It
writes desktop and mobile comparison screenshots to `web/test-results/`.

### Sync status and Details

The shared toolbar uses a cloud status icon for synced, pending, offline, or
attention states. Its tooltip and accessible label describe the current status.
The editor still reports local save failures directly.

The sync icon opens a **Sync details** dialog from the toolbar and separates **Sync** (pending changes, latest completed check in this
session, retry, and dates needing attention) from **Offline availability** (app
readiness, history completion, and the number of locally stored entries with a
nonzero server revision). The download count is not a percentage or a server total;
local-only drafts are excluded. Dates that have not downloaded may already contain
server writing.

**Storage and recovery** is collapsed during normal use and contains backup and
storage-protection actions. It opens automatically for a storage message. Sign-in is shown only when authentication is needed.
