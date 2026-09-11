# JWT sessions

Logbook keeps signed HS256 JWTs. The browser receives them only through the
`__Host-logbook-session` cookie: Secure, HttpOnly, SameSite=Strict, Path=/,
with no Domain attribute. Responses and localStorage do not contain JWTs.
Authorization bearer headers are no longer accepted.

Each JWT has a random `jti`. The database stores its SHA-256 hash in
`auth_session`, never the raw token. Every authenticated request checks the
signature, issuer, audience, expiry, session record, and current active user and
role. Login rotates the previous browser session; logout revokes it before
clearing the cookie. Deactivation revokes all sessions for that user. These checks
work across backend processes and restarts.

Sessions expire after 12 hours absolutely, or 30 minutes without authenticated
API activity. Background sync counts as activity; this is not a keyboard-idle
timer. The separate vault still locks according to its own shorter timer.

## Browser requests

The frontend and API must share one origin. `GET /api/session` returns session
metadata and a derived CSRF token, held only in frontend memory. Anonymous
bootstrap uses a separate Secure HttpOnly cookie. Login, registration, logout,
and all protected API requests require `X-CSRF-Token`. Binding protected reads
and writes to the current session also rejects requests from a stale tab after
the shared cookie changes to another account.

Unsafe requests require the expected Origin; cross-site requests are rejected.
Production assumes HTTPS and a reverse proxy preserving Host. If the proxy
rewrites Host, set `LOGBOOK_PUBLIC_ORIGIN` to the exact public origin, such as
`https://logbook.example.com`. Forwarded headers are not implicitly trusted.
Loopback development supports localhost HTTP with a browser that accepts Secure
cookies on localhost. API responses are marked no-store.

## Offline behavior

The existing account namespace and IndexedDB diary data are preserved. Old
localStorage JWTs are removed; their claims may recover the old local namespace
but never authenticate a request. Users sign in again to resume server access.

Offline logout clears local access immediately and persists a pending logout
intent. The app retries server revocation while open after reconnecting and on
reload; it never restores a pending session. Until the server receives logout,
a copied cookie remains valid until expiry or revocation. Closing the app cannot
complete a network request. Local diary partitions remain on the device.

## Deployment

1. Apply DbUp migration `0006_jwt_sessions.sql` using the normal
   [migration procedure](database-migrations.md) before starting the new API.
2. Deploy the API and frontend together behind the same HTTPS origin. Configure
   `LOGBOOK_PUBLIC_ORIGIN` if Host is rewritten by the proxy.
3. Existing sessions require a fresh login. Reload open tabs to load the new app;
   older bearer clients must adopt the cookie/bootstrap/header flow.
4. Verify login, diary sync, vault access, and logout through the public origin.

The migration and application are tested locally; this change does not apply
production migrations or deploy the service. Existing JWT signing configuration
is retained. Further password hashing upgrades, throttling, and MFA are separate
work. HttpOnly cookies do not prevent malicious same-origin JavaScript from
acting through an unlocked application.
