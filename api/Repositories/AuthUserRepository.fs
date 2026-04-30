module AuthUserRepository

open Npgsql

let existsByEmail (conn: NpgsqlConnection) email =
    AuthUser.CheckUserExists conn email

let existsById (conn: NpgsqlConnection) userId =
    AuthUser.CheckActiveUserExistsByID conn userId

let getByEmail (conn: NpgsqlConnection) email =
    AuthUser.GetUserByEmail conn email

let tryGetByEmail (conn: NpgsqlConnection) email =
    try
        Some(AuthUser.GetUserByEmail conn email)
    with :? NoResultsException ->
        None

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

let deactivateById (conn: NpgsqlConnection) userId =
    AuthUser.DeactivateAuthUserByID conn userId > 0
