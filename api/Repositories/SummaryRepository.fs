module SummaryRepository

open Npgsql

let getByUserId (conn: NpgsqlConnection) userId =
    Summary.GetSummaryByUserId conn userId

let getByUserAndNoteId (conn: NpgsqlConnection) userId noteId =
    Summary.GetSummaryByUserIDAndID conn { NoteId = noteId; UserId = userId }

let insertOrUpdate (conn: NpgsqlConnection) noteId userId content =
    Summary.InsertSummary
        conn
        { NoteId = noteId
          UserId = userId
          Content = content }
    |> ignore

let lastUpdated (conn: NpgsqlConnection) userId noteId =
    Summary.LastUpdated conn { NoteId = noteId; UserId = userId }
