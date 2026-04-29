module ApiPaths

let apiPrefix = "/api"

let login = "/api/login"
let logout = "/api/logout"

let diaryIds = "/api/diary_ids"
let diary = "/api/diary"
let diarySearch = "/api/diary/search"
let diaryById = "/api/diary/{id}"

let usersWithDiaryCount = "/api/users/with-diary"
let userById = "/api/users/{id}"
let todo = "/api/todo"

let exportJson = "/api/export_json"
let exportMarkdown = "/api/export_md"
let exportAll = "/api/export_all"

let publicApiPaths =
    [ login ]
