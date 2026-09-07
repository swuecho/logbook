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

// Versioned endpoints used by the offline client. Revisions travel as strings.
let syncChanges: HttpHandler =
    fun ctx ->
        let request = HandlerContext.authenticated ctx
        match Int64.TryParse(HandlerContext.queryValue "cursor" "0" ctx) with
        | true, cursor when cursor >= 0L ->
            DiarySyncService.changes request.DbSession request.UserId cursor |> HandlerResponse.json ctx
        | _ -> HandlerResponse.jsonWithStatus 400 {| Message = "Invalid sync cursor." |} ctx

let syncGet: HttpHandler =
    fun ctx ->
        let request = HandlerContext.authenticated ctx
        withValidNoteId (HandlerContext.routeValue "id" "" ctx) (fun noteId ->
            DiarySyncService.get request.DbSession request.UserId noteId |> HandlerResponse.jsonHandler) ctx

let syncSave: HttpHandler =
    fun ctx ->
        let request = HandlerContext.authenticated ctx
        let publisher = ctx.RequestServices.GetRequiredService<ApplicationContracts.IBackgroundJobPublisher>()
        withValidNoteId (HandlerContext.routeValue "id" "" ctx) (fun noteId ->
            Json.Request.mapJson (fun (mutation: DiarySyncService.Mutation) ->
                match DiarySyncService.save request.DbSession publisher request.UserId noteId mutation with
                | DiarySyncService.Saved entry -> HandlerResponse.jsonHandler entry
                | DiarySyncService.Conflict entry -> HandlerResponse.jsonWithStatus 409 {| Current = entry |}
                | DiarySyncService.InvalidMutation -> HandlerResponse.jsonWithStatus 400 {| Message = "Invalid sync mutation." |})) ctx
