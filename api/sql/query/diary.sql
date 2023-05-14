-- name: ListDiaries :many
SELECT * FROM diary ORDER BY last_updated DESC;

-- name: DiaryByID :one
SELECT * FROM diary WHERE id = $1;

-- name: ListDiaryByUserID :many
SELECT * FROM diary WHERE user_id = $1;

-- name: DiaryByUserIDAndID :one
SELECT * FROM diary WHERE user_id = $1 and id=$2;

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
FROM (select * from diary d0 where d0.user_id = $1)  d
LEFT JOIN summary s ON d.id = s.id AND d.user_id = s.user_id
WHERE s.id IS NULL OR d.last_updated > s.last_updated;
