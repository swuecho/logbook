
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
    last_updated timestamp with time zone DEFAULT NOW() NOT NULL
);

-- note_id and user_id are unique together
CREATE UNIQUE INDEX IF NOT EXISTS diary_id_user_id_idx ON diary (note_id, user_id);

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


