module ExportService

open Database

let diaryJson (db: DbSession) userId noteId =
    db.WithConnection(fun conn -> DiaryRepository.getByUserAndNoteId conn userId noteId)

let allDiaries (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listByUserId conn userId)

let diaryMarkdown (db: DbSession) userId noteId =
    db.WithConnection(fun conn ->
        let diary = DiaryRepository.getByUserAndNoteId conn userId noteId
        TipTap.tipTapDocJsonToMarkdown diary.Note)
