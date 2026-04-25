module DiaryHandlers

open Falco

let listDiaryIds: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId)
            ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (DiaryService.refreshAndListSummaries requestContext.DbSession requestContext.UserId)
            ctx

let getById: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")

        Json.Response.ofJson
            (DiaryService.getOrCreateDiary requestContext.DbSession requestContext.UserId noteId)
            ctx

let save: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Request.mapJson
            (fun (note: Diary) ->
                DiaryService.saveDiary requestContext.DbSession requestContext.UserId note
                |> Json.Response.ofJson)
            ctx

let search: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        let query =
            match ctx.Request.Query.TryGetValue("q") with
            | true, value -> value.ToString()
            | false, _ -> ""

        Json.Response.ofJson
            (DiaryService.search requestContext.DbSession requestContext.UserId query)
            ctx

let todoLists: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (DiaryService.todoDocument requestContext.DbSession requestContext.UserId)
            ctx
