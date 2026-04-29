module AuthUserRepository

open Npgsql

let private addIntParameter (cmd: NpgsqlCommand) (name: string) (value: int) =
    cmd.Parameters.AddWithValue(name, value) |> ignore

let existsByEmail (conn: NpgsqlConnection) email =
    AuthUser.CheckUserExists conn email

let existsById (conn: NpgsqlConnection) userId =
    use cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM auth_user WHERE id = @user_id AND is_active = true);", conn)
    addIntParameter cmd "user_id" userId
    cmd.ExecuteScalar() :?> bool

let getByEmail (conn: NpgsqlConnection) email =
    AuthUser.GetUserByEmail conn email

let create (conn: NpgsqlConnection) email password firstName lastName username isStaff isSuperuser =
    let user: AuthUser.CreateAuthUserParams =
        { Email = email
          Password = password
          FirstName = firstName
          LastName = lastName
          Username = username
          IsStaff = isStaff
          IsSuperuser = isSuperuser }

    AuthUser.CreateAuthUser conn user

let updateLastLogin (conn: NpgsqlConnection) userId =
    AuthUser.UpdateLastLogin conn userId |> ignore

let getUsersWithDiaryCount (conn: NpgsqlConnection) =
    AuthUser.GetUsersWithDiaryCount conn

let deleteByIdWithData (conn: NpgsqlConnection) userId =
    use cmd =
        new NpgsqlCommand(
            """
            DELETE FROM todo WHERE user_id = @user_id;
            DELETE FROM summary WHERE user_id = @user_id;
            DELETE FROM diary WHERE user_id = @user_id;
            DELETE FROM auth_user WHERE id = @user_id;
            """,
            conn
        )

    addIntParameter cmd "user_id" userId

    cmd.ExecuteNonQuery() > 0
