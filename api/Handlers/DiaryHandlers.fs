module DiaryHandlers

open Falco
open Microsoft.AspNetCore.Http
open Database.Connection

let private currentUserId (ctx: HttpContext) =
    HttpAuth.getUserId ctx.User

let listDiaryIds: HttpHandler =
    fun ctx ->
        let conn = ctx.GetNpgsqlConnection()
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.listDiaryIds conn userId) ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let conn = ctx.GetNpgsqlConnection()
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.listSummaries conn userId) ctx

let getById: HttpHandler =
    fun ctx ->
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")
        let conn = ctx.GetNpgsqlConnection()
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.getOrCreateDiary conn userId noteId) ctx

let save: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let conn = ctx.GetNpgsqlConnection()
                let userId = currentUserId ctx

                DiaryService.saveDiary conn userId note
                |> Json.Response.ofJson)
            ctx

let search: HttpHandler =
    fun ctx ->
        let query =
            match ctx.Request.Query.TryGetValue("q") with
            | true, value -> value.ToString()
            | false, _ -> ""

        let conn = ctx.GetNpgsqlConnection()
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.search conn userId query) ctx

let todoLists: HttpHandler =
    fun ctx ->
        let conn = ctx.GetNpgsqlConnection()
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.todoDocument conn userId) ctx
