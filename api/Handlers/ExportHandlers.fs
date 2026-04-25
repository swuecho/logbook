module ExportHandlers

open Falco
open Database.Connection

type ExportDiaryRequest = { Id: string }

let exportDiary: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HttpAuth.getUserId ctx.User

                ExportService.diaryJson (dbSession ctx) userId request.Id
                |> Json.Response.ofJson)
            ctx

let exportAllDiaries: HttpHandler =
    fun ctx ->
        let userId = HttpAuth.getUserId ctx.User

        Json.Response.ofJson (ExportService.allDiaries (dbSession ctx) userId) ctx

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HttpAuth.getUserId ctx.User

                let markdown = ExportService.diaryMarkdown (dbSession ctx) userId request.Id
                Response.ofPlainText markdown)
            ctx
