module DiaryHandlers

open Falco
open Microsoft.AspNetCore.Http
open Database.Connection

let private currentUserId (ctx: HttpContext) =
    HttpAuth.getUserId ctx.User

let listDiaryIds: HttpHandler =
    fun ctx ->
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.listDiaryIds (dbSession ctx) userId) ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.listSummaries (dbSession ctx) userId) ctx

let getById: HttpHandler =
    fun ctx ->
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.getOrCreateDiary (dbSession ctx) userId noteId) ctx

let save: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let userId = currentUserId ctx

                DiaryService.saveDiary (dbSession ctx) userId note
                |> Json.Response.ofJson)
            ctx

let search: HttpHandler =
    fun ctx ->
        let query =
            match ctx.Request.Query.TryGetValue("q") with
            | true, value -> value.ToString()
            | false, _ -> ""

        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.search (dbSession ctx) userId query) ctx

let todoLists: HttpHandler =
    fun ctx ->
        let userId = currentUserId ctx

        Json.Response.ofJson (DiaryService.todoDocument (dbSession ctx) userId) ctx
