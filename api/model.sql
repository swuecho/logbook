
CREATE TABLE public.diary (
    id character varying(8) NOT NULL,
    note text
    last_updated timestamp with timezone DEFAULT NOW(),
);

ALTER TABLE diary ADD COLUMN IF NOT EXISTS     last_updated timestamp with time zone DEFAULT NOW();
CREATE TABLE summary (
    id character varying(8) NOT NULL,
    created_at timestamp with time zone DEFAULT NOW(),
    last_updated timestamp with time zone DEFAULT NOW(),
    content jsonb,
    PRIMARY KEY(id),
    CONSTRAINT fk_diary FOREIGN KEY(id)  REFERENCES diary(id)
);


