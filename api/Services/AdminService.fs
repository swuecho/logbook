module AdminService

open Database

type DeleteUserResult =
    | UserDeleted
    | UserNotFound
    | CannotDeleteSelf

let usersWithDiaryCount (db: DbSession) =
    db.WithConnection AuthUserRepository.getUsersWithDiaryCount

let deleteUser (db: DbSession) requestingUserId targetUserId =
    if requestingUserId = targetUserId then
        CannotDeleteSelf
    else
        db.WithTransaction(fun conn ->
            if AuthUserRepository.deleteByIdWithData conn targetUserId then
                UserDeleted
            else
                UserNotFound)
