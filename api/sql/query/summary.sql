-- name: GetSummaryByUserId :many
select id, content as note from summary where user_id = $1 order by id desc;

-- name: GetSummaryByUserIDAndID :one
SELECT * FROM summary WHERE user_id = $1 and id=$2;