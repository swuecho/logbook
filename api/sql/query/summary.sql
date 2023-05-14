-- name: GetSummaryByUserId :many
select id, content as note from summary where user_id = $1 order by id desc;

-- name: GetSummaryByUserIDAndID :one
SELECT * FROM summary WHERE user_id = $1 and id=$2;


-- name: LastUpdated :one
select last_updated from summary where id = $1 and user_id = $2   ;

-- name: InsertSummary :exec
insert INTO summary (id, user_id, content, last_updated) 
VALUES ($1, $2, $3, now()) 
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, last_updated =  EXCLUDED.last_updated;
