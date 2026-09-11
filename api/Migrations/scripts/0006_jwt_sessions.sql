-- JWT signatures authenticate the token; this allowlist makes it revocable.
CREATE TABLE IF NOT EXISTS auth_session (
    token_hash VARCHAR(64) PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES auth_user(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS auth_session_user_idx ON auth_session (user_id);
CREATE INDEX IF NOT EXISTS auth_session_expiry_idx ON auth_session (expires_at);
