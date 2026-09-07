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
