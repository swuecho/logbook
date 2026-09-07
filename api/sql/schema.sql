
CREATE TABLE IF NOT EXISTS jwt_secrets (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    secret TEXT NOT NULL,
    audience TEXT NOT NULL
);



CREATE TABLE IF NOT EXISTS auth_user (
  id SERIAL PRIMARY KEY,
  password VARCHAR(128) NOT NULL,
  last_login TIMESTAMP default now() NOT NULL,
  is_superuser BOOLEAN default false NOT NULL,
  username VARCHAR(150) UNIQUE NOT NULL,
  first_name VARCHAR(30) default '' NOT NULL,
  last_name VARCHAR(30) default '' NOT NULL,
  email VARCHAR(254) UNIQUE NOT NULL,
  is_staff BOOLEAN default false NOT NULL,
  is_active BOOLEAN default true NOT NULL,
  date_joined TIMESTAMP default now() NOT NULL
);

-- add index on email
CREATE INDEX IF NOT EXISTS auth_user_email_idx ON auth_user (email);

CREATE TABLE IF NOT EXISTS diary (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES auth_user(id),
    note_id character varying(8) NOT NULL,
    note text DEFAULT '' NOT NULL,
    search_text text DEFAULT '' NOT NULL,
    search_terms text[] DEFAULT ARRAY[]::text[] NOT NULL,
    last_updated timestamp with time zone DEFAULT NOW() NOT NULL
);

ALTER TABLE diary ADD COLUMN IF NOT EXISTS search_text text DEFAULT '' NOT NULL;
ALTER TABLE diary ADD COLUMN IF NOT EXISTS search_terms text[] DEFAULT ARRAY[]::text[] NOT NULL;

-- note_id and user_id are unique together
CREATE UNIQUE INDEX IF NOT EXISTS diary_id_user_id_idx ON diary (note_id, user_id);
CREATE INDEX IF NOT EXISTS diary_user_note_id_idx ON diary (user_id, note_id DESC);
CREATE INDEX IF NOT EXISTS diary_search_terms_idx ON diary USING GIN (search_terms);

CREATE TABLE IF NOT EXISTS summary (
    id SERIAL PRIMARY KEY,
    note_id character varying(8) NOT NULL,
    user_id INTEGER NOT NULL REFERENCES auth_user(id),
    created_at timestamp with time zone DEFAULT NOW() NOT NULL,
    last_updated timestamp with time zone DEFAULT NOW() NOT NULL,
    content jsonb DEFAULT '{}' NOT NULL
);

-- note_id and user_id are unique together
CREATE UNIQUE INDEX IF NOT EXISTS summary_id_user_id_idx ON summary (note_id, user_id);

CREATE TABLE IF NOT EXISTS todo (
    note_id character varying(8) NOT NULL,
    user_id INTEGER NOT NULL REFERENCES auth_user(id),
    todos jsonb DEFAULT '[]'::jsonb NOT NULL,
    PRIMARY KEY (note_id, user_id)
);

CREATE INDEX IF NOT EXISTS todo_user_note_id_idx ON todo (user_id, note_id DESC);

-- A per-user counter is locked until commit, so a sync cursor cannot skip
-- a smaller revision from a transaction that commits later.
CREATE TABLE IF NOT EXISTS diary_sync_state (
    user_id INTEGER PRIMARY KEY REFERENCES auth_user(id) ON DELETE CASCADE,
    revision BIGINT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS diary_sync_entry (
    user_id INTEGER NOT NULL REFERENCES auth_user(id) ON DELETE CASCADE,
    note_id VARCHAR(8) NOT NULL,
    note TEXT NOT NULL,
    revision BIGINT NOT NULL,
    PRIMARY KEY (user_id, note_id)
);
CREATE INDEX IF NOT EXISTS diary_sync_revision_idx ON diary_sync_entry (user_id, revision);

CREATE TABLE IF NOT EXISTS diary_sync_receipt (
    user_id INTEGER NOT NULL REFERENCES auth_user(id) ON DELETE CASCADE,
    mutation_id UUID NOT NULL,
    note_id VARCHAR(8) NOT NULL,
    requested_hash VARCHAR(64) NOT NULL,
    base_revision BIGINT NOT NULL,
    is_empty BOOLEAN NOT NULL,
    revision BIGINT NOT NULL,
    PRIMARY KEY (user_id, mutation_id)
);

CREATE OR REPLACE FUNCTION record_diary_sync() RETURNS trigger AS $$
DECLARE next_revision BIGINT;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.note = OLD.note AND EXISTS (
            SELECT 1 FROM diary_sync_entry WHERE user_id = NEW.user_id AND note_id = NEW.note_id
        ) THEN
            RETURN NEW;
        END IF;
    END IF;
    INSERT INTO diary_sync_state (user_id, revision) VALUES (NEW.user_id, 1)
    ON CONFLICT (user_id) DO UPDATE SET revision = diary_sync_state.revision + 1
    RETURNING revision INTO next_revision;

    INSERT INTO diary_sync_entry (user_id, note_id, note, revision)
    VALUES (NEW.user_id, NEW.note_id, NEW.note, next_revision)
    ON CONFLICT (user_id, note_id) DO UPDATE
    SET note = EXCLUDED.note, revision = EXCLUDED.revision;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS diary_sync_changed ON diary;
CREATE TRIGGER diary_sync_changed AFTER INSERT OR UPDATE OF note ON diary
FOR EACH ROW EXECUTE FUNCTION record_diary_sync();

-- Backfill existing diaries, including empty entries, without changing dates.
UPDATE diary SET note = note WHERE NOT EXISTS (
    SELECT 1 FROM diary_sync_entry s WHERE s.user_id = diary.user_id AND s.note_id = diary.note_id
);
