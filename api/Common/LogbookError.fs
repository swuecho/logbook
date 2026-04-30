namespace Logbook

type ApiError =
    { Code: string
      Message: string }

/// Standard API error bodies (HTTP status is set by the handler; keep codes machine-readable, messages human-readable).
module HttpError =
    let authenticationRequired: ApiError =
        { Code = "unauthorized"
          Message = "Authentication is required to access this resource." }

    let accessDenied: ApiError =
        { Code = "forbidden"
          Message = "Access to the resource is forbidden." }

    let invalidCredentials: ApiError =
        { Code = "invalid_credentials"
          Message = "Login failed. password or email is wrong" }

    let invalidNoteId: ApiError =
        { Code = "invalid_note_id"
          Message = "Diary note id must be an 8-digit date in YYYYMMDD format." }

    let invalidUserId: ApiError =
        { Code = "invalid_user_id"
          Message = "User id must be a valid integer." }

    let userNotFound: ApiError =
        { Code = "user_not_found"
          Message = "User was not found." }

    let cannotDeleteSelf: ApiError =
        { Code = "cannot_delete_self"
          Message = "You cannot delete your own user account." }

    let emailAlreadyRegistered: ApiError =
        { Code = "email_already_registered"
          Message = "An account with that email already exists. Please log in instead." }
