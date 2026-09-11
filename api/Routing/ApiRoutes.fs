module ApiRoutes

open Falco.Routing

let endpoints =
    [
        get ApiPaths.session AuthHandlers.session
        post ApiPaths.login AuthHandlers.login
        post ApiPaths.register AuthHandlers.register
        post ApiPaths.logout AuthHandlers.logout
        get "/api/vault" VaultHandlers.get
        put "/api/vault" VaultHandlers.save
        get "/api/sync/changes" DiaryHandlers.syncChanges
        get "/api/sync/diary/{id}" DiaryHandlers.syncGet
        put "/api/sync/diary/{id}" DiaryHandlers.syncSave
        get ApiPaths.diaryIds DiaryHandlers.listDiaryIds
        get ApiPaths.usersWithDiaryCount AdminHandlers.usersWithDiaryCount
        delete ApiPaths.userById AdminHandlers.deleteUser
        get ApiPaths.diary DiaryHandlers.listSummaries
        get ApiPaths.diarySearch DiaryHandlers.search
        get ApiPaths.diaryById DiaryHandlers.getById
        put ApiPaths.diaryById DiaryHandlers.save
        get ApiPaths.todo DiaryHandlers.todoLists
        post ApiPaths.exportJson ExportHandlers.exportDiary
        post ApiPaths.exportMarkdown ExportHandlers.exportDiaryMarkdown
        get ApiPaths.exportAll ExportHandlers.exportAllDiaries
    ]
