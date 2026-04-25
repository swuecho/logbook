module ApiRoutes

open Falco.Routing

let endpoints =
    [
        post "/api/login" AuthHandlers.login
        post "/api/logout" AuthHandlers.logout
        get "/api/diary_ids" DiaryHandlers.listDiaryIds
        get "/api/users/with-diary" AdminHandlers.usersWithDiaryCount
        get "/api/diary" DiaryHandlers.listSummaries
        get "/api/diary/search" DiaryHandlers.search
        get "/api/diary/{id}" DiaryHandlers.getById
        put "/api/diary/{id}" DiaryHandlers.save
        get "/api/todo" DiaryHandlers.todoLists
        post "/api/export_json" ExportHandlers.exportDiary
        post "/api/export_md" ExportHandlers.exportDiaryMarkdown
        get "/api/export_all" ExportHandlers.exportAllDiaries
    ]
