module DiaryRepository

open Npgsql

let private searchTermSeparator = "\u001F"

let private joinSearchTerms (terms: string array) =
    terms |> String.concat searchTermSeparator

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

let listWithTodo (conn: NpgsqlConnection) =
    Diary.ListDiaryWithTodo conn

let listWithTodoByUserId (conn: NpgsqlConnection) userId =
    Diary.ListDiaryWithTodoByUserID conn userId

let listIdsByUserId (conn: NpgsqlConnection) userId =
    Diary.ListDiaryIDByUserID conn userId

let search (conn: NpgsqlConnection) userId terms =
    Diary.SearchDiary
        conn
        { UserId = userId
          QueryTerms = Some(joinSearchTerms terms)
          Separator = searchTermSeparator }

let updateSearchIndex (conn: NpgsqlConnection) noteId userId searchText searchTerms =
    Diary.UpdateDiarySearch
        conn
        { NoteId = noteId
          UserId = userId
          SearchText = searchText
          SearchTerms = Some(joinSearchTerms searchTerms)
          Separator = searchTermSeparator }
    |> ignore

let listMissingSearchIndex (conn: NpgsqlConnection) =
    Diary.ListMissingSearchIndex conn

let listStaleSummaryIdsByUserId (conn: NpgsqlConnection) userId =
    Diary.GetStaleIdsOfUserId conn userId

let listStaleSummaryIds (conn: NpgsqlConnection) =
    Diary.ListStaleSummaryIds conn

let isSummaryStale (conn: NpgsqlConnection) userId noteId =
    Diary.CheckIdStale conn { NoteId = noteId; UserId = userId }
