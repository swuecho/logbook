module AdminService

open Npgsql

let usersWithDiaryCount (conn: NpgsqlConnection) =
    AuthUser.GetUsersWithDiaryCount conn
