-- name: GetTodoByUserId :many
SELECT note_id, todos FROM todo WHERE user_id = $1 ORDER BY note_id DESC;

-- name: InsertOrUpdateTodo :exec
INSERT INTO todo (note_id, user_id, todos)
VALUES ($1, $2, $3)
ON CONFLICT (note_id, user_id) DO UPDATE SET todos = EXCLUDED.todos;

-- name: DeleteTodo :exec
DELETE FROM todo WHERE note_id = $1 AND user_id = $2;

-- name: DeleteStaleTodos :exec
-- Deletes todo rows whose source diary note no longer contains todo/task nodes,
-- or whose diary row no longer exists (e.g. deleted note).
DELETE FROM todo t
WHERE NOT EXISTS (
  SELECT 1
  FROM diary d
  WHERE d.note_id = t.note_id
    AND d.user_id = t.user_id
    AND d.note != ''
    AND (
      d.note LIKE '%todo_list%'
      OR d.note LIKE '%todo_item%'
      OR d.note LIKE '%taskList%'
      OR d.note LIKE '%taskItem%'
    )
);
