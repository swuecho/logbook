module ExportService

open Database

let exportDiaryJson (db: DbSession) userId noteId =
    db.WithConnection(fun conn -> DiaryRepository.getByUserAndNoteId conn userId noteId)

let exportAllDiaries (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listByUserId conn userId)

let exportDiaryMarkdown (db: DbSession) userId noteId =
    db.WithConnection(fun conn ->
        let diary = DiaryRepository.getByUserAndNoteId conn userId noteId
        TipTap.tipTapDocJsonToMarkdown diary.Note)
