module DiaryHandlers

open Falco

let listDiaryIds: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.refreshAndListSummaries requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx

let getById: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")

        DiaryService.getOrCreateDiary requestContext.DbSession requestContext.UserId noteId
        |> HandlerResponse.json ctx

let save: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Request.mapJson
            (fun (note: Diary) ->
                DiaryService.saveDiary requestContext.DbSession requestContext.UserId note
                |> HandlerResponse.jsonHandler)
            ctx

let search: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        let query =
            match ctx.Request.Query.TryGetValue("q") with
            | true, value -> value.ToString()
            | false, _ -> ""

        DiaryService.search requestContext.DbSession requestContext.UserId query
        |> HandlerResponse.json ctx

let todoLists: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.todoDocument requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx
