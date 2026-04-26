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
