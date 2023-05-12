
CREATE TABLE IF NOT EXISTS diary (
    id character varying(8) NOT NULL,
    note text DEFAULT '' NOT NULL,
    last_updated timestamp with time zone DEFAULT NOW() NOT NULL
);

CREATE TABLE IF NOT EXISTS summary (
    id character varying(8) NOT NULL,
    created_at timestamp with time zone DEFAULT NOW() NOT NULL,
    last_updated timestamp with time zone DEFAULT NOW() NOT NULL,
    content jsonb DEFAULT '{}' NOT NULL,
    PRIMARY KEY(id),
    CONSTRAINT fk_diary FOREIGN KEY(id)  REFERENCES diary(id)
);


