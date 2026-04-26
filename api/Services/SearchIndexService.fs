module SearchIndexService

open Npgsql

let updateSearchIndex (conn: NpgsqlConnection) noteId userId note =
    let searchText, terms = TextAnalysis.searchIndexOfNote note
    DiaryRepository.updateSearchIndex conn noteId userId searchText terms
    searchText, terms

let refreshSearchIndex (conn: NpgsqlConnection) =
    DiaryRepository.listMissingSearchIndex conn
    |> List.iter (fun diary -> updateSearchIndex conn diary.NoteId diary.UserId diary.Note |> ignore)
