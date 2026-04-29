module DiaryHandlers

open Falco
open Microsoft.Extensions.DependencyInjection

let listDiaryIds: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.listDiaryIds requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx

let listSummaries: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.listSummaries requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx

let getById: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx
        let noteId = HandlerContext.routeValue "id" "" ctx

        DiaryService.getOrCreateDiary requestContext.DbSession requestContext.UserId noteId
        |> HandlerResponse.json ctx

let save: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx
        let publisher = ctx.RequestServices.GetRequiredService<ApplicationContracts.IBackgroundJobPublisher>()

        Json.Request.mapJson
            (fun (note: Diary) ->
                DiaryService.saveDiary requestContext.DbSession publisher requestContext.UserId note
                |> HandlerResponse.jsonHandler)
            ctx

let search: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        let query = HandlerContext.queryValue "q" "" ctx

        DiaryService.search requestContext.DbSession requestContext.UserId query
        |> HandlerResponse.json ctx

let todoLists: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        DiaryService.todoDocument requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx
