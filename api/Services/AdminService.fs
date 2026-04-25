module AdminService

open Npgsql

let usersWithDiaryCount (conn: NpgsqlConnection) =
    AuthUserRepository.getUsersWithDiaryCount conn
