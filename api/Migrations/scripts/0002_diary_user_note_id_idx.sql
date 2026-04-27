CREATE INDEX IF NOT EXISTS diary_user_note_id_idx ON diary (user_id, note_id DESC);
