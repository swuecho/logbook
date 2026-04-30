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
            if AuthUserRepository.deactivateById conn targetUserId then
                UserRevocationCache.markDeactivated targetUserId
                UserDeleted
            else
                UserNotFound)
