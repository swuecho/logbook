-- name: ListDiaries :many
SELECT * FROM diary ORDER BY last_updated DESC;

-- name: DiaryByID :one
SELECT * FROM diary WHERE id = $1;

-- name: ListDiaryByUserID :many
SELECT * FROM diary WHERE user_id = $1;

-- name: DiaryByUserIDAndID :one
SELECT * FROM diary WHERE user_id = $1 and note_id=$2;

-- name: CreateDiary :one
INSERT INTO diary (id, note) VALUES ($1, $2)
RETURNING *;

-- name: UpdateDiary :one
UPDATE diary SET note = $2, last_updated = NOW() WHERE id = $1
RETURNING *;

-- name: DeleteDiary :exec
DELETE FROM diary WHERE id = $1;

-- name: GetStaleIdsOfUserId :many
SELECT d.*
FROM diary d
LEFT JOIN summary s ON d.id = s.id AND d.user_id = s.user_id AND d.user_id = $1
WHERE s.id IS NULL OR d.last_updated > s.last_updated;


-- name: AddNote :one
INSERT INTO diary (note_id, user_id, note, last_updated) VALUES ($1, $2, $3, now())  
ON CONFLICT (note_id, user_id) DO UPDATE SET note = EXCLUDED.note, last_updated =  EXCLUDED.last_updated
returning *;

-- name: CheckIdStale :one
SELECT count(*)  > 0 as stale
FROM diary d
LEFT JOIN summary s ON d.id = s.id AND d.user_id = s.user_id AND d.user_id = $1 AND d.note_id = $2
WHERE s.id IS NULL OR d.last_updated > s.last_updated;