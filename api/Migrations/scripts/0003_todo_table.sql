CREATE TABLE IF NOT EXISTS todo (
    note_id character varying(8) NOT NULL,
    user_id INTEGER NOT NULL REFERENCES auth_user(id),
    todos jsonb DEFAULT '[]'::jsonb NOT NULL,
    PRIMARY KEY (note_id, user_id)
);

CREATE INDEX IF NOT EXISTS todo_user_note_id_idx ON todo (user_id, note_id DESC);
