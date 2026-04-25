module ExportHandlers

open Falco

type ExportDiaryRequest = { Id: string }

let exportDiary: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                ExportService.exportDiaryJson requestContext.DbSession requestContext.UserId request.Id
                |> Json.Response.ofJson)
            ctx

let exportAllDiaries: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Response.ofJson
            (ExportService.exportAllDiaries requestContext.DbSession requestContext.UserId)
            ctx

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let markdown =
                    ExportService.exportDiaryMarkdown requestContext.DbSession requestContext.UserId request.Id

                Response.ofPlainText markdown)
            ctx
