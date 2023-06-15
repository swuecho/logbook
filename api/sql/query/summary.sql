-- name: GetSummaryByUserId :many
select id, content as note from summary where user_id = $1 order by id desc;

-- name: GetSummaryByUserIDAndID :one
SELECT * FROM summary WHERE user_id = $1 and note_id=$2;


-- name: LastUpdated :one
select last_updated from summary where note_id = $1 and user_id = $2   ;

-- name: InsertSummary :exec
insert INTO summary (note_id, user_id, content, last_updated) 
VALUES ($1, $2, $3, now()) 
ON CONFLICT (note_id, user_id) DO UPDATE SET content = EXCLUDED.content, last_updated =  EXCLUDED.last_updated;
