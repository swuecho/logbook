-- name: GetTodoByUserId :many
SELECT note_id, todos FROM todo WHERE user_id = $1 ORDER BY note_id DESC;

-- name: InsertOrUpdateTodo :exec
INSERT INTO todo (note_id, user_id, todos)
VALUES ($1, $2, $3)
ON CONFLICT (note_id, user_id) DO UPDATE SET todos = EXCLUDED.todos;

-- name: DeleteTodo :exec
DELETE FROM todo WHERE note_id = $1 AND user_id = $2;
