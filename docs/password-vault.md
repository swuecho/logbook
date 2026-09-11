# Password and secret vault

Implemented September 2026. JWT session hardening is implemented; see [authentication](authentication.md). Further account hardening remains separate.

## Use

Open the lock icon in the navigation bar, or `/vault`, while signed in. Create a separate master passphrase (at least 15 characters), save the generated recovery key outside Logbook, and confirm it is saved. The server receives neither credential.

The vault supports logins, arbitrary secrets (including multiline private keys through Reveal), and secure notes. Titles, usernames, URLs, secret values, notes and tags are encrypted. Search runs locally over titles, usernames, URLs and tags. Secret values are masked until explicitly revealed; notes are visible while editing. The generator creates 24-character passwords using cryptographic randomness and rejection sampling.

Save edits explicitly. The vault locks after five minutes without pointer/keyboard/touch activity, on hiding the page, leaving the route, reload, logout/session changes, and lock notifications from other tabs. Locking discards unsaved edits and clears key references, credentials, recovery-key display and editor state. Clipboard history is not controlled or reliably erased by the application.

Encrypted backup downloads a versioned JSON envelope, including the current editor's contents. It requires the master passphrase or recovery key that existed when exported. Keep backups separately from recovery keys. To restore, unlock a destination vault (create it first if needed), choose Restore backup, provide the backup credential, and explicitly confirm replacement. The browser decrypts and validates the backup, then re-encrypts all items using the destination vault keys. No source-account identifiers or source server are needed.

Use the recovery key to unlock when the passphrase is forgotten. Change keys creates a new master envelope, data key and recovery key, re-encrypts all saved items, and saves the entire result atomically after recovery-key confirmation. Old credentials cannot decrypt the current vault afterward; old backups still accept their original credentials. Without either credential, the contents cannot be recovered by resetting the account password.

## Storage and encryption

This first release uses a single encrypted document per account, rather than separate item rows, to keep saves, restores and full key rotation atomic. It is limited to 1 MB of plaintext / 2,000 items, with 100,000-character secret/note fields. This is for a small personal vault, not large attachments, sharing or machine-to-machine secret delivery.

- Version 1 uses native Web Crypto PBKDF2-HMAC-SHA256 with exactly 600,000 iterations, a fresh random 16-byte salt, and a 256-bit derived wrapping key. Using native PBKDF2 instead of the proposed Argon2id avoids an additional JavaScript/WASM cryptography dependency. The format is versioned so a future KDF can be introduced deliberately. Parameters are validated before expensive derivation.
- A random 256-bit data key encrypts the complete item array with AES-256-GCM. A new random 96-bit nonce is generated for every encryption. Authentication tags are 128 bits.
- The master wrapping key and an independent random 256-bit recovery key each encrypt a copy of the data key. The recovery key is displayed as grouped hexadecimal, with 256 bits of randomness.
- Authenticated additional data binds format version, an immutable vault encryption UUID and purpose (`master`, `recovery`, `payload`). The master envelope also binds KDF settings. This UUID remains portable across accounts; authorization uses the authenticated account separately.
- CryptoKeys are nonextractable. Temporary raw byte arrays are cleared where possible; keys and plaintext are otherwise kept only in the mounted view's memory. JavaScript/OS memory erasure cannot be guaranteed.
- PostgreSQL `password_vault` stores owner ID, opaque envelope, monotonically increasing revision and update timestamp. It contains no master verifier or plaintext item metadata. Size/timing and account ownership remain visible to the server.
- `GET /api/vault` and `PUT /api/vault` always use the authenticated owner; the API offers no admin override or user-ID selector. PUT accepts `{ envelope: string, baseRevision: string }`; revision `0` initializes. Every update is conditional on the expected revision. An exact retry of the last mutation succeeds without incrementing the revision again. Other stale writes receive 409.
- Requests and responses are bounded. Responses use `Cache-Control: no-store`; fetch uses `cache: no-store`. Existing service-worker code excludes `/api/` from caching. The vault never uses diary IndexedDB, summaries, search indexing, sync queues or diary exports. No new dependency or plaintext logging was introduced.

No offline vault cache is provided. Concurrent saves require reloading the latest document and reapplying an edit; the UI keeps a failed editor and supports encrypted backup before locking. Loss of a response can leave a save's outcome uncertain; locking and unlocking reads the authoritative state. An old backup or malicious server can replay an older complete envelope: local monotonic anti-rollback state is not implemented.

## Deployment

1. Back up the target database through the existing operational process.
2. Run the existing DbUp migration command with the target environment's `DATABASE_URL`: `dotnet run --project api/Migrations/Migrations.fsproj`. Migration `0005_password_vault.sql` creates the isolated table. Do not replace production schema with the test initializer.
3. Build the frontend (`npm --prefix web run build`, or the existing Yarn equivalent) and backend, then deploy through the usual process. HTTPS is required outside localhost.
4. Test creation, save/reload, encrypted export and recovery with a synthetic item. Download and test a backup in a fresh account/browser before depending on it.

No production migration or deployment was performed as part of implementation. Rolling back application code leaves the new table intact. Keep it and backups until retention/deletion decisions are explicit. Item deletion removes it from the current encrypted snapshot, not older backups. Deleting the account row cascades to the vault; the existing admin deactivation merely disables the account.

## Verification

- `npm --prefix web test`: crypto round trips, independent Node PBKDF2 interoperability, a standard AES-256-GCM empty-message vector, tamper/wrong-key rejection, nonce freshness, key rotation, portable restore, format/size limits and existing offline-store tests.
- `dotnet test api/tests/unit.fsproj`: backend unit and disposable PostgreSQL integration tests, including owner isolation, no-store, initialization retry, concurrent-write conflicts, invalid/oversized input and diary export separation.
- `cd web && npx playwright test`: existing offline regressions plus vault creation, save, encrypted export, recovery, restore, key rotation, storage inspection, desktop/mobile layout, safe URLs, failed writes, automatic locking and interrupted unlock.
- `cd web && LOGBOOK_REAL_SYNC_TEST=1 npx playwright test`: existing real diary-sync tests and a vault test against the actual API and a disposable database. The harness runs migrations twice and checks the migration journal, then removes the test container. Real browser ciphertext is accepted by the API and independently decrypted with the recovery key; the service-worker cache is checked for API responses.

On this macOS environment, .NET required `DOTNET_EnableWriteXorExecute=0`; local browser/test servers and Docker required execution outside the filesystem sandbox. Chrome can be selected using `PLAYWRIGHT_CHROMIUM_EXECUTABLE`.

## Remaining boundaries

Encryption protects stored content against database/backup disclosure subject to passphrase strength. It does not protect an unlocked vault from XSS, malicious extensions, a compromised device, or a server delivering malicious JavaScript. Account sessions now use HttpOnly JWT cookies with database revocation and CSRF protection. Stolen account access can still retrieve ciphertext or overwrite it without decrypting it. Password hashing upgrades, sign-in throttling, and MFA remain separate work.

The implementation has automated coverage, not an independent cryptographic audit or production penetration test. Tests currently report an existing high-severity SSH.NET advisory in the backend test dependency graph; the application build itself does not report that dependency warning. No dependency upgrades are included here.
