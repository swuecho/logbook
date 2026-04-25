module ExportService

open Npgsql

let diaryJson (conn: NpgsqlConnection) userId noteId =
    DiaryRepository.getByUserAndNoteId conn userId noteId

let allDiaries (conn: NpgsqlConnection) userId =
    DiaryRepository.listByUserId conn userId

let diaryMarkdown (conn: NpgsqlConnection) userId noteId =
    let diary = diaryJson conn userId noteId
    TipTap.tipTapDocJsonToMarkdown diary.Note
