-- name: ListDiaries :many
SELECT id, user_id, note_id, note, last_updated FROM diary ORDER BY last_updated DESC;

-- name: DiaryByID :one
SELECT id, user_id, note_id, note, last_updated FROM diary WHERE id = $1;

-- name: ListDiaryByUserID :many
SELECT id, user_id, note_id, note, last_updated FROM diary WHERE user_id = $1 order by note_id DESC;

-- name: ListDiaryIDByUserID :many
SELECT note_id FROM diary WHERE user_id = $1 AND note != '' order by note_id DESC;

-- name: DiaryByUserIDAndID :one
SELECT id, user_id, note_id, note, last_updated FROM diary WHERE user_id = $1 and note_id=$2;

-- name: CreateDiary :one
INSERT INTO diary (id, note) VALUES ($1, $2)
RETURNING id, user_id, note_id, note, last_updated;

-- name: UpdateDiary :one
UPDATE diary SET note = $2, last_updated = NOW() WHERE id = $1
RETURNING id, user_id, note_id, note, last_updated;

-- name: DeleteDiary :exec
DELETE FROM diary WHERE id = $1;

-- name: GetStaleIdsOfUserId :many
SELECT d.id, d.user_id, d.note_id, d.note, d.last_updated
FROM diary d
LEFT JOIN summary s ON d.id = s.id AND d.user_id = s.user_id 
WHERE (s.id IS NULL OR d.last_updated > s.last_updated) AND d.user_id = $1;


-- name: AddNote :one
INSERT INTO diary (note_id, user_id, note, last_updated) VALUES ($1, $2, $3, now())  
ON CONFLICT (note_id, user_id) DO UPDATE SET note = EXCLUDED.note, last_updated =  EXCLUDED.last_updated
returning id, user_id, note_id, note, last_updated;

-- name: UpdateDiarySearch :exec
UPDATE diary
SET search_text = $3,
    search_terms =
        CASE
            WHEN $4 = '' THEN ARRAY[]::text[]
            ELSE string_to_array($4, $5)
        END
WHERE note_id = $1 AND user_id = $2;

-- name: SearchDiary :many
WITH query_terms AS (
  SELECT
    CASE
      WHEN $2 = '' THEN ARRAY[]::text[]
      ELSE string_to_array($2, $3)
    END AS terms
)
SELECT d.note_id, d.search_text, d.last_updated,
       (
         SELECT COUNT(*)
         FROM unnest(d.search_terms) term
         WHERE term = ANY(query_terms.terms)
       )::int AS rank
FROM diary d
CROSS JOIN query_terms
WHERE d.user_id = $1
  AND d.search_terms && query_terms.terms
ORDER BY rank DESC, d.last_updated DESC, d.note_id DESC;

-- name: ListMissingSearchIndex :many
SELECT id, user_id, note_id, note, last_updated
FROM diary
WHERE note != '' AND (search_text = '' OR search_terms = ARRAY[]::text[]);

-- name: CheckIdStale :one
SELECT count(*)  > 0 as stale
FROM diary d
LEFT JOIN summary s ON d.id = s.id AND d.user_id = s.user_id AND d.user_id = $1 AND d.note_id = $2
WHERE s.id IS NULL OR d.last_updated > s.last_updated;
