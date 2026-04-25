module DiaryHandlers

open Falco

let listDiaryIds: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (DiaryService.listDiaryIds (HandlerContext.dbSession ctx) userId) ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (DiaryService.refreshAndListSummaries (HandlerContext.dbSession ctx) userId) ctx

let getById: HttpHandler =
    fun ctx ->
        let route = Request.getRoute ctx
        let noteId = route.GetString("id", "")
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (DiaryService.getOrCreateDiary (HandlerContext.dbSession ctx) userId noteId) ctx

let save: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (note: Diary) ->
                let userId = HandlerContext.userId ctx

                DiaryService.saveDiary (HandlerContext.dbSession ctx) userId note
                |> Json.Response.ofJson)
            ctx

let search: HttpHandler =
    fun ctx ->
        let query =
            match ctx.Request.Query.TryGetValue("q") with
            | true, value -> value.ToString()
            | false, _ -> ""

        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (DiaryService.search (HandlerContext.dbSession ctx) userId query) ctx

let todoLists: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (DiaryService.todoDocument (HandlerContext.dbSession ctx) userId) ctx
