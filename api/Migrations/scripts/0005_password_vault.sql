-- The server stores only the opaque encrypted vault envelope.
CREATE TABLE IF NOT EXISTS password_vault (
    user_id INTEGER PRIMARY KEY REFERENCES auth_user(id) ON DELETE CASCADE,
    envelope TEXT NOT NULL CHECK (octet_length(envelope) <= 1500000),
    revision BIGINT NOT NULL DEFAULT 1 CHECK (revision > 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
