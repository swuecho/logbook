module ExportHandlers

open Falco

type ExportDiaryRequest = { Id: string }

let exportDiary: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                ExportService.exportDiaryJson requestContext.DbSession requestContext.UserId request.Id
                |> HandlerResponse.jsonHandler)
            ctx

let exportAllDiaries: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        ExportService.exportAllDiaries requestContext.DbSession requestContext.UserId
        |> HandlerResponse.json ctx

let exportDiaryMarkdown: HttpHandler =
    fun ctx ->
        let requestContext = HandlerContext.authenticated ctx

        Json.Request.mapJson
            (fun (request: ExportDiaryRequest) ->
                let markdown =
                    ExportService.exportDiaryMarkdown requestContext.DbSession requestContext.UserId request.Id

                HandlerResponse.plainText markdown)
            ctx
