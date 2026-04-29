module DiaryHandlers

open System
open System.Globalization
open Falco
open Logbook
open Microsoft.Extensions.DependencyInjection

let private isValidNoteId (noteId: string) =
    let mutable parsed = DateTime.MinValue

    not (String.IsNullOrWhiteSpace noteId)
    && DateTime.TryParseExact(
        noteId,
        "yyyyMMdd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        &parsed
    )

let private withValidNoteId noteId handler =
    if isValidNoteId noteId then
        handler noteId
    else
        HandlerResponse.clientError 400 HttpError.invalidNoteId

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

        withValidNoteId
            noteId
            (fun noteId ->
                fun ctx ->
                    DiaryService.getOrCreateDiary requestContext.DbSession requestContext.UserId noteId
                    |> HandlerResponse.json ctx)
            ctx

let save: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx
        let publisher = ctx.RequestServices.GetRequiredService<ApplicationContracts.IBackgroundJobPublisher>()
        let noteId = HandlerContext.routeValue "id" "" ctx

        withValidNoteId
            noteId
            (fun noteId ->
                Json.Request.mapJson (fun (note: Diary) ->
                    let noteForRoute = { note with NoteId = noteId }

                    DiaryService.saveDiary requestContext.DbSession publisher requestContext.UserId noteForRoute
                    |> HandlerResponse.jsonHandler))
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
