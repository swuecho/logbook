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
        let userId = getUserId ctx.User
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Diary.ListDiaryByUserID conn userId
                |> List.map (Jieba.freqsOfNote conn)
                |> Json.Response.ofJson)
            ctx

let noteAllPart: HttpHandler =
    fun ctx ->
        // refresh note summary
        let userId = getUserId ctx.User
        let conn = ctx.getNpgsql ()

        Request.mapRoute
            (ignore)
            (fun _ ->
                Jieba.refreshSummary conn userId
                Summary.GetSummaryByUserId conn userId
                |> List.filter (fun x -> x.Note.Length > 2)
                |> Json.Response.ofJson)
            ctx



let noteByIdPart: HttpHandler =
    fun ctx ->
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")
        let userId = getUserId ctx.User
        let conn = ctx.getNpgsql ()

        try
            let diary = Diary.DiaryByUserIDAndID conn { NoteId = noteId; UserId = userId }
            Json.Response.ofJson diary ctx
        with :? NoResultsException as ex ->
            Json.Response.ofJson
                (Diary.AddNote
                    conn
                    { NoteId = noteId
                      UserId = userId
                      Note = "" })
                ctx

let addNotePart: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let userId = getUserId ctx.User
                let conn = ctx.getNpgsql ()

                Diary.AddNote
                    conn
                    { NoteId = note.NoteId
                      UserId = userId
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
    let passwordHash = Auth.generatePasswordHash password

    let newUser: AuthUser.CreateAuthUserParams =
        { Email = email
          Password = passwordHash
          FirstName = ""
          LastName = ""
          Username = email
          IsStaff = false
          IsSuperuser = false }

    let authUser = AuthUser.CreateAuthUser conn newUser
    authUser


let login: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (login: Login) ->
                let conn = ctx.getNpgsql ()
                let email = login.Username
                let password = login.Password
                let jwtAudienceName = "logbook"
                let secret = JwtSecrets.GetJwtSecret conn jwtAudienceName
                let jwtKey = secret.Secret
                let audience = secret.Audience
                let issuer = "logbook-swuecho.github.com"
                // try get user if not exist create one
                if AuthUser.CheckUserExists conn email then
                    let user = AuthUser.GetUserByEmail conn email
                    let hash = user.Password
                    // check if password matches
                    let passwordMatches = Auth.validatePassword password hash
                    // check if user is admin
                    let role = if user.IsSuperuser then "admin" else "user"

                    if passwordMatches then
                        let jwt = Token.generateToken user.Id role jwtKey audience issuer
                        Json.Response.ofJson jwt
                    else
                        // return failure
                        Response.withStatusCode (int HttpStatusCode.Unauthorized)
                        >> Response.ofJson
                            {| code = HttpStatusCode.Unauthorized
                               message = "Login failed. password or email is wrong" |}
                else
                    let authUser = createNewUser conn email password
                    let jwt = Token.generateToken authUser.Id "user" jwtKey audience issuer
                    Json.Response.ofJson jwt
            ) 
            ctx

let logout: HttpHandler =
    fun ctx ->
        let userId = getUserId ctx.User
        printfn "User with ID %d logged out" userId
        Response.ofJson {| message = "Logged out successfully" |} ctx


let extractTodoLists allDiary =
    allDiary
    |> List.choose (fun diary ->
        let todoList = TipTap.extractTodoList diary.Note

        if List.isEmpty todoList then
            None
        else
            Some
                {| noteId = diary.NoteId
                   todoList = todoList |})


let todoListsHandler: HttpHandler =
    fun ctx ->
        let conn = ctx.getNpgsql ()
        let userId = getUserId ctx.User
        let allDiary = Diary.ListDiaryByUserID conn userId
        let todoLists = extractTodoLists allDiary
        let tiptapDoc = TipTap.constructTipTapDoc todoLists
        Json.Response.ofJson tiptapDoc ctx



let listDiaryIds: HttpHandler =
    fun ctx ->
        let conn = ctx.getNpgsql ()
        let userId = getUserId ctx.User
        let diaryIds = Diary.ListDiaryIDByUserID conn userId 
        Json.Response.ofJson diaryIds ctx

let exportDiary: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: {| Id: string |}) ->
                let conn = ctx.getNpgsql ()
                let userId = getUserId ctx.User
                let diary = Diary.DiaryByUserIDAndID conn { NoteId = note.Id; UserId = userId }
                Json.Response.ofJson diary)
            ctx
let exportAllDiaries: HttpHandler =
    fun ctx ->
        let conn = ctx.getNpgsql ()
        let userId = getUserId ctx.User
        let allDiary = Diary.ListDiaryByUserID conn userId
        Json.Response.ofJson allDiary ctx

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: {| Id: string |}) ->
                let conn = ctx.getNpgsql ()
                let userId = getUserId ctx.User
                let diary = Diary.DiaryByUserIDAndID conn { NoteId = note.Id; UserId = userId }
                let markdown = TipTap.tipTapDocJsonToMarkdown diary.Note
                Response.ofPlainText markdown)
            ctx

let getUsersWithDiaryCount : HttpHandler =
    fun ctx ->
        // let conn = ctx.RequestServices.GetRequiredService<NpgsqlConnection>()
        // check user is superuser
        let conn = ctx.getNpgsql ()
        let user = ctx.User
        if user.IsInRole "admin" then
            let users = AuthUser.GetUsersWithDiaryCount conn
            Json.Response.ofJson users ctx
        else
            forbidden ctx