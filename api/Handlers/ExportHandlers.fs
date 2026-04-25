module ExportHandlers

open Falco
open Database.Connection

type ExportDiaryRequest = { Id: string }

let exportDiary: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HttpAuth.getUserId ctx.User

                withConnection ctx (fun conn ->
                    ExportService.diaryJson conn userId request.Id
                    |> Json.Response.ofJson))
            ctx

let exportAllDiaries: HttpHandler =
    fun ctx ->
        let userId = HttpAuth.getUserId ctx.User

        withConnection ctx (fun conn ->
            Json.Response.ofJson (ExportService.allDiaries conn userId) ctx)

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HttpAuth.getUserId ctx.User

                withConnection ctx (fun conn ->
                    let markdown = ExportService.diaryMarkdown conn userId request.Id
                    Response.ofPlainText markdown))
            ctx
