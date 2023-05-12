-- name: ListDiaries :many
SELECT * FROM diary ORDER BY last_updated DESC;

-- name: DiaryByID :one
SELECT * FROM diary WHERE id = $1;

-- name: CreateDiary :one
INSERT INTO diary (id, note) VALUES ($1, $2)
RETURNING *;

-- name: UpdateDiary :one
UPDATE diary SET note = $2, last_updated = NOW() WHERE id = $1
RETURNING *;

-- name: DeleteDiary :exec
DELETE FROM diary WHERE id = $1;