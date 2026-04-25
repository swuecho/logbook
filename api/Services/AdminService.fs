module AdminService

open Database

let usersWithDiaryCount (db: DbSession) =
    db.WithConnection(AuthUserRepository.getUsersWithDiaryCount)
