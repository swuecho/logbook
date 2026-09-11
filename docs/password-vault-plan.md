# Authentication review and password/secret vault plan

Date: 2026-09-11. Status: proposed; no application changes made.

## Assessment

The current authentication has sound primitives, but is not ready to protect a password manager. Fix session lifecycle, login protection, and account revocation before enabling a vault.

Scope: static review of backend authentication, route middleware, repositories, frontend sessions, offline storage, deployment configuration, and existing tests. This is not a production penetration test or dependency audit. No production endpoints were probed, credentials tested, or test suites executed. Deployment settings and actual secret strength remain unverified.

### Existing strengths

- `api/Common/Auth.fs` uses PBKDF2-HMAC-SHA256, a random 16-byte salt, a 32-byte hash, and constant-time comparison.
- `api/Startup/AppStartup.fs` validates JWT signature, expiry, issuer, and audience, and requires authentication and a user ID for non-public `/api` paths.
- Admin handlers check the admin role. Reviewed diary, sync, and export queries scope data by authenticated user ID and use parameterized SQL.
- Request logging records method, path, status, time, and user ID rather than request bodies.
- `api/fly.toml` configures HTTPS redirection. This establishes configuration intent, not proof of the live deployment.

### Findings

| Priority | Evidence | Risk and required change |
| --- | --- | --- |
| High | `api/Common/Token.fs`, `web/src/services/session.ts`, `api/Handlers/AuthHandlers.fs` | JWTs last seven days, persist in localStorage, and logout only returns a message. A copied token remains usable after logout. Replace with revocable server sessions and a Secure, HttpOnly cookie; enforce expiry and CSRF protection. |
| High | `api/Services/AuthService.fs`, `api/Program.fs`, `web/src/views/Login.vue` | Registration has no server-side password/email validation; the UI minimum is five characters and is bypassable. No application login throttling or MFA was found. Add bounded input validation, strong password policy, rate limits and MFA. Edge limits, if any, were not verified. |
| High | `api/Services/AuthService.fs`, `api/Infrastructure/UserRevocationCache.fs`, `api/Startup/AppStartup.fs` | Login does not check IsActive. Protected requests reject IDs in a process-local denylist, rather than positively checking current account/session status. Deactivation on another instance and role changes can leave existing tokens effective. Use authoritative active-account, session and role checks across instances. On the same instance, the existing denylist does block deactivated users' protected requests. |
| High, conditional | `api/tests/data/20241224.json:5`, `docker-compose.yml` | A fixture contains a credential-bearing database URL; compose includes a fixed database password and publishes PostgreSQL on the host. Treat the fixture credential as exposed if real: rotate it, replace it with synthetic data, and assess history. Keep production DB access private and use deployment secrets. Credential validity was not tested. |
| Medium | `api/Infrastructure/JwtService.fs`, `api/Startup/AppStartup.fs` | JWT signing material is stored in the application DB. A DB read compromise can enable token forgery. Existing DB values override environment changes, making rotation surprising. Remove this dependency when migrating to opaque sessions; explicitly invalidate legacy tokens. |
| Medium | `api/Common/Auth.fs` | 260,000 PBKDF2 iterations is below current OWASP SHA-256 guidance of 600,000. Verification requires the exact current count, so simply raising the constant breaks existing logins. Parse bounded, versioned hashes, safely reject malformed hashes, and rehash after successful legacy login. |
| Medium | `api/Startup/AppStartup.fs`, `api/Program.fs` | CORS allows every origin in all environments. No application CSP/HSTS configuration was found. Restrict production origins and verify deployment headers. Permissive CORS alone does not let another origin read a user's localStorage token. |
| Medium | `api/Services/AuthService.fs`, `web/src/views/Login.vue` | Registration reveals existing emails; absent-user login skips password hashing, creating a timing difference. Frontend logs entire Axios errors, which can contain submitted passwords in request config. Use controlled error handling, safe diagnostic fields, and comparable verification work. |

Browser script injection is especially consequential with localStorage tokens. The reviewed search snippet escapes HTML before highlighting; this review did not establish an exploitable XSS bug. Rich-text links, media, dependencies and service-worker behavior still need targeted review.

## Proposed scope and security boundary

A personal vault for logins, API tokens, private keys, and secure notes. One vault per account, with a separate master passphrase. Account login authorizes access to encrypted records; unlocking decrypts them in the browser. Administrators cannot obtain plaintext through an application endpoint.

This protects stored contents against a database/backup disclosure, subject to master-passphrase strength. It does not protect an unlocked vault from malicious browser extensions, device compromise, XSS, or a compromised server delivering hostile JavaScript. Do not claim complete protection from the hosting server.

Start online-only, with no persistent browser vault cache. Defer sharing, autofill/extensions, attachments, TOTP generation, and automated machine-to-machine secret delivery. This is storage for personal secrets, not infrastructure secret injection or rotation.

## Implementation sequence

### 1. Harden authentication first

- Replace browser-stored JWTs with random opaque session IDs in a host-only `__Host-` Secure, HttpOnly, SameSite cookie. Store only session-token hashes in PostgreSQL, with user ID, creation, idle/absolute expiry and revocation. Rotate on login and privilege changes. Proposed defaults: 30-minute idle and 12-hour absolute expiry, enforced by the server.
- Revoke on logout, password reset/change, account deactivation, and explicit sign-out-all. Check active status and current authorization on every request. Expire existing JWTs through a deliberate cutover; remove JWT storage and signing-key DB records when rollback no longer needs them.
- Add CSRF tokens and Origin checks to mutations, including login; use same-origin production requests. Update Axios, background diary sync and account switching together. Replace client JWT decoding with `/api/session` account information; preserve diary account partitioning and unsynced work.
- Validate email and password on the server. For password-only login, use a 15-character minimum for new passwords, permit long passphrases/paste, block common compromised passwords, and cap input size without truncation. Do not impose the new minimum on legacy login before migration.
- Upgrade PBKDF2-SHA256 to at least 600,000 iterations after benchmarking. Support the current 260,000 format with safe parsing and opportunistic rehash. Argon2id is an alternative if a maintained compatible implementation is selected.
- Add per-account and per-IP throttling with shared enforcement across instances, bounded delays and safe audit events. Avoid permanent lockout as an attacker-controlled denial of service.
- Add MFA before storing real secrets; TOTP with hashed one-time recovery codes is a practical initial option. Require recent authentication for MFA/account recovery changes. Keep MFA recovery separate from vault recovery.
- Restrict registration to owner/invited accounts if this is a private deployment; otherwise add verified-email enrollment and a designed account-recovery flow.
- Resolve exposed credentials, production DB access, HTTPS/HSTS, CSP, safe errors and CORS. Audit rich-text input and URL handling without claiming CSP alone prevents XSS.

Exit checks: forged/expired sessions fail; logout invalidates a copied session; deactivation and role changes work across two instances and restarts; CSRF and throttling tests pass; legacy passwords migrate; offline diary sync survives the session migration.

### 2. Specify and test the encryption format

- Generate a random 256-bit vault data key in the browser. Derive a wrapping key from the separate master passphrase using a maintained Argon2id implementation, with random salt and stored, versioned, bounded parameters. Benchmark supported devices and document the chosen settings before implementation.
- Wrap the data key with authenticated encryption. Encrypt each complete item using AES-256-GCM through Web Crypto and a fresh random 96-bit nonce for every write. Authenticate immutable vault/item identifiers and format version as additional data. Never reuse a nonce/key pair or substitute account password hashes/JWT secrets for encryption keys.
- Encrypt title, URL, username, password, notes, tags and custom fields. The server sees only opaque IDs, ciphertext, nonces, revisions and necessary timestamps/ownership; item counts, sizes and access timing remain visible. Search locally after unlock.
- Store unwrapped keys only in memory. Lock on explicit action, logout, account switch, reload and inactivity (proposed five minutes); notify other tabs to lock without transmitting keys. Clear UI state and key references; JavaScript cannot guarantee physical memory erasure.
- Master-passphrase change rewraps the data key atomically after verifying the old passphrase. A suspected data-key compromise requires key rotation and re-encryption of every item.
- Offer an optional high-entropy recovery key that independently wraps the data key, with a confirmation step showing it was saved. Without the master passphrase or recovery key, contents cannot be recovered. Account-password reset must not decrypt or silently destroy the vault.

Exit checks: published crypto test vectors, tamper/wrong-key/identifier-swap rejection, nonce handling, malformed envelope limits, passphrase change, recovery and key rotation are tested. Obtain focused independent cryptographic design review before real-secret use.

### 3. Add a separate encrypted backend

- Add migrations for `vaults` (owner, crypto/KDF version, salt, wrapped data key and optional recovery envelope) and `vault_items` (vault/item IDs, nonce, ciphertext, revision and deletion state). Enforce uniqueness, foreign keys and payload limits.
- Add `VaultRepository`, `VaultService`, and `VaultHandlers` alongside existing modules. Derive ownership exclusively from the session; scope all queries and mutations by owner. Admin role must not bypass vault ownership.
- Expose vault initialization, encrypted item list/read/create/update/delete and atomic key-envelope updates under `/api/vault`. Require revision preconditions for edits/deletes and idempotency for retryable creates. Return conflicts rather than silently overwriting secrets.
- Keep these tables and endpoints outside diary summaries, search indexes, exports, background jobs and plaintext sync. Return `Cache-Control: no-store`; ensure service workers never cache vault/API responses. Never log item plaintext, keys, request bodies or auth headers.

Exit checks: two-user isolation for every operation, oversized/malformed input rejection, conflict/retry behavior, and no vault contents in diary/export/log paths.

### 4. Build the quiet vault interface

- Add a Vault navigation item and setup/unlock/locked states, then a compact searchable list and detail editor using shared `ui.css` tokens and Element Plus.
- Support Login, Secret, and Secure Note templates; create/edit/delete, masked fields, explicit reveal/copy, tags, and local search. Display secret text as text, never rich HTML. Only allow safe external URL schemes.
- Generate passwords locally using cryptographically secure randomness and unbiased selection. Do not reuse the existing modulo-based 12-character generator.
- Provide a visible Lock action. Hide revealed values on lock/navigation. Copy only on request; any timed clipboard clearing is best effort and must not imply clipboard history is erased.
- Keep unlocked contents out of persistent storage, analytics, console errors, URLs and application-wide state. Review route/code isolation and the existing service worker before release.

Exit checks: reload/idle/logout/account switch lock correctly; storage inspection finds no plaintext secrets or unwrapped keys; reveal/copy and keyboard flows work; diary/todo behavior remains intact.

### 5. Recovery, backup and release gate

- Provide encrypted vault export/import with versioned envelopes and validation. Export must include the key envelopes needed to restore; test recovery in a fresh browser/account context without relying on IDs that change on import. Account for old backups remaining decryptable with old keys/passphrases.
- Test server backup restoration, failed writes, concurrent edits, lost master passphrase with/without recovery, and interrupted key rotation. Define deletion and backup-retention behavior clearly.
- Run backend unit/integration tests and frontend build/E2E tests, audit dependencies, inspect production TLS/headers/session behavior, and perform focused XSS/authorization/crypto review. Use synthetic secrets throughout development.
- Enable the feature only after the auth and encryption exit checks pass. Keep later offline support ciphertext-only, with its own design for synchronization, conflicts and device revocation limitations.

## References

- [OWASP Password Storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html): KDF selection and work factors.
- [OWASP Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html): password policy, throttling, MFA and safe error responses.
- [OWASP Session Management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html) and [HTML5 Security](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html): session lifecycle, cookies and browser storage.
- [OWASP Cryptographic Storage](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html) and [Key Management](https://cheatsheetseries.owasp.org/cheatsheets/Key_Management_Cheat_Sheet.html): authenticated encryption, key separation and recovery.
