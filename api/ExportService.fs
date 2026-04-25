module ExportService

open Npgsql

let diaryJson (conn: NpgsqlConnection) userId noteId =
    Diary.DiaryByUserIDAndID conn { NoteId = noteId; UserId = userId }

let allDiaries (conn: NpgsqlConnection) userId =
    Diary.ListDiaryByUserID conn userId

let diaryMarkdown (conn: NpgsqlConnection) userId noteId =
    let diary = diaryJson conn userId noteId
    TipTap.tipTapDocJsonToMarkdown diary.Note
