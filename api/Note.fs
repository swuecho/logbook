module Note

open System.Net
open Microsoft.AspNetCore.Http
open Falco
open System.Security.Claims
open Npgsql

let forbidden =
    let message = "Access to the resource is forbidden."

    Response.ofJson
        {| code = HttpStatusCode.Forbidden
           message = message |}

let getUserId (user: ClaimsPrincipal) = int (user.FindFirst("user_id").Value)


let AuthRequired h = Request.ifAuthenticated h forbidden

/// from async objec to json response


type HttpContext with

    member this.getNpgsql() =
        // implementation of your method
        match this.Items.["NpgsqlConnection"] with
        | :? NpgsqlConnection as connection -> connection
        | _ -> failwith "can not get connection"


// https://github.com/pimbrouwers/Falco/pull/41
// do not cache result
let noteAllPartSlow: HttpHandler =
    fun ctx ->
        let user_id = getUserId ctx.User
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Diary.ListDiaryByUserID conn user_id
                |> List.map (Jieba.freqsOfNote conn)
                |> Json.Response.ofJson)
            ctx

let noteAllPart: HttpHandler =
    fun ctx ->
        // refresh note summary
        let user_id = int (ctx.User.FindFirst("user_id").Value)
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Jieba.refreshSummary conn user_id
                Summary.GetSummaryByUserId conn user_id |> Json.Response.ofJson)
            ctx



let noteByIdPart: HttpHandler =
    fun ctx ->
        let route = Request.getRoute ctx
        let note_id = route.GetString("id", "")
        let user_id = getUserId ctx.User

        let conn = ctx.getNpgsql ()


        try
            let diary = Diary.DiaryByUserIDAndID conn { Id = note_id; UserId = user_id }
            Json.Response.ofJson diary ctx
        with :? NoResultsException as ex ->
            Json.Response.ofJson
                (Diary.AddNote
                    conn
                    { Id = note_id
                      UserId = user_id
                      Note = "" })
                ctx
// Response.withStatusCode 404 ctx |>
// Response.ofJson
//     {| code = 401
//        message = sprintf "No diary found for user %d with id %s" user_id note_id |}

//Request.mapRoute getSurvey Json.Response.ofJson ctx

let addNotePart: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let user_id = int (ctx.User.FindFirst("user_id").Value)
                let conn = ctx.getNpgsql ()

                Diary.AddNote
                    conn
                    { Id = note.Id
                      UserId = user_id
                      Note = note.Note }
                |> Json.Response.ofJson)
            ctx

let noteByIdPartDebug: HttpHandler =
    fun ctx ->
        let principal = ctx.User

        for claim in principal.Claims do
            printfn "%s" ("CLAIM TYPE: " + claim.Type + "; CLAIM VALUE: " + claim.Value)

        printfn "%A" ctx.User |> ignore
        noteByIdPart ctx

type Login = { Username: string; Password: string }


let createNewUser conn email password =
    // User does not exist, create a new user
    let newUser: AuthUser.CreateAuthUserParams =
        { Email = email
          Password = password
          FirstName = ""
          LastName = ""
          Username = email
          IsStaff = false
          IsSuperuser = false }

    let authUser = AuthUser.CreateAuthUser conn newUser

    // Set the role for the new user
    let role = "user"

    let jwtSecret = JwtSecrets.GetJwtSecret conn "logbook"
    let issuer = "logbook-swuecho.github.com"

    let jwt =
        Token.generateToken authUser.Id role jwtSecret.Secret jwtSecret.Audience issuer

    jwt

let login: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (login: Login) ->
                let conn = ctx.getNpgsql ()
                let email = login.Username
                let password = login.Password
                // try get user if not exist create one
                try 
                    let user = AuthUser.GetUserByEmail conn email
                    let hash = user.Password
                    // check if password matches
                    let passwordMatches = Auth.validatePassword password hash
                    // check if user is admin
                    let role = if user.IsSuperuser then "admin" else "user"

                    let jwtSecret = JwtSecrets.GetJwtSecret conn "logbook"
                    let issuer = "logbook-swuecho.github.com"

                    if passwordMatches then
                        let jwt =
                            Token.generateToken user.Id role jwtSecret.Secret jwtSecret.Audience issuer

                        Json.Response.ofJson jwt
                    else
                        // return failure
                        Json.Response.ofJson {| code = HttpStatusCode.Unauthorized
                                                message = "Login failed." |}
                with
                |  ex ->
                    let jwt = createNewUser conn email password
                    Json.Response.ofJson jwt
            )


            ctx
