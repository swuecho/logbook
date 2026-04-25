module ExportHandlers

open Falco

type ExportDiaryRequest = { Id: string }

let exportDiary: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HandlerContext.userId ctx

                ExportService.diaryJson (HandlerContext.dbSession ctx) userId request.Id
                |> Json.Response.ofJson)
            ctx

let exportAllDiaries: HttpHandler =
    fun ctx ->
        let userId = HandlerContext.userId ctx

        Json.Response.ofJson (ExportService.allDiaries (HandlerContext.dbSession ctx) userId) ctx

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let userId = HandlerContext.userId ctx

                let markdown = ExportService.diaryMarkdown (HandlerContext.dbSession ctx) userId request.Id
                Response.ofPlainText markdown)
            ctx
