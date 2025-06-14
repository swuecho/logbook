-- name: GetAllAuthUsers :many
SELECT * FROM auth_user ORDER BY id;

-- name: ListAuthUsers :many
SELECT * FROM auth_user ORDER BY id LIMIT $1 OFFSET $2;

-- name: GetAuthUserByID :one
SELECT * FROM auth_user WHERE id = $1;


-- name: GetAuthUserByEmail :one
SELECT * FROM auth_user WHERE email = $1;

-- name: CreateAuthUser :one
INSERT INTO auth_user (email, "password", first_name, last_name, username, is_staff, is_superuser)
VALUES ($1, $2, $3, $4, $5, $6, $7)
RETURNING *;

-- name: UpdateAuthUser :one
UPDATE auth_user SET first_name = $2, last_name= $3, last_login = now() 
WHERE id = $1
RETURNING first_name, last_name, email;

-- name: UpdateAuthUserByEmail :one
UPDATE auth_user SET first_name = $2, last_name= $3, last_login = now() 
WHERE email = $1
RETURNING first_name, last_name, email;

-- name: DeleteAuthUser :exec
DELETE FROM auth_user WHERE email = $1;

-- name: GetUserByEmail :one
SELECT * FROM auth_user WHERE email = $1;

-- name: CheckUserExists :one
SELECT EXISTS(SELECT 1 FROM auth_user WHERE email = $1);

-- name: UpdateUserPassword :exec
UPDATE auth_user SET "password" = $2 WHERE email = $1;

-- name: GetTotalActiveUserCount :one
SELECT COUNT(*) FROM auth_user WHERE is_active = true;

-- name: GetUsersWithDiaryCount :many
SELECT 
    au.id,
    au.email,
    au.first_name,
    au.last_name,
    au.date_joined,
    au.last_login,
    COUNT(d.id) as diary_count
FROM auth_user au
LEFT JOIN diary d ON au.id = d.user_id
GROUP BY au.id
ORDER BY au.date_joined DESC;

-- name: UpdateLastLogin :exec
UPDATE auth_user SET last_login = now()  WHERE id = $1;