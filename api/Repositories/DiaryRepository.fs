module DiaryRepository

open Npgsql

let addOrUpdate (conn: NpgsqlConnection) noteId userId note =
    Diary.AddNote
        conn
        { NoteId = noteId
          UserId = userId
          Note = note }

let getByUserAndNoteId (conn: NpgsqlConnection) userId noteId =
    Diary.DiaryByUserIDAndID conn { NoteId = noteId; UserId = userId }

let listByUserId (conn: NpgsqlConnection) userId =
    Diary.ListDiaryByUserID conn userId

let listIdsByUserId (conn: NpgsqlConnection) userId =
    Diary.ListDiaryIDByUserID conn userId

let search (conn: NpgsqlConnection) userId terms =
    Diary.SearchDiary conn { UserId = userId; QueryTerms = terms }

let updateSearchIndex (conn: NpgsqlConnection) noteId userId searchText searchTerms =
    Diary.UpdateDiarySearch
        conn
        { NoteId = noteId
          UserId = userId
          SearchText = searchText
          SearchTerms = searchTerms }
    |> ignore

let listMissingSearchIndex (conn: NpgsqlConnection) =
    Diary.ListMissingSearchIndex conn

let listStaleSummaryIdsByUserId (conn: NpgsqlConnection) userId =
    Diary.GetStaleIdsOfUserId conn userId

let isSummaryStale (conn: NpgsqlConnection) userId noteId =
    Diary.CheckIdStale conn { NoteId = noteId; UserId = userId }
