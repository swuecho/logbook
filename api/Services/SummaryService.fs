module SummaryService

open Npgsql

let summaryJson (note: Diary) =
    note.Note |> TipTap.getTextFromNote |> TextAnalysis.freqs |> Json.Convert.toJson

let updateNoteSummary (conn: NpgsqlConnection) noteId userId =
    let summary =
        DiaryRepository.getByUserAndNoteId conn userId noteId
        |> summaryJson

    SummaryRepository.insertOrUpdate conn noteId userId summary

let refreshSummary (conn: NpgsqlConnection) userId =
    DiaryRepository.listStaleSummaryIdsByUserId conn userId
    |> List.map (fun x -> x.NoteId)
    |> List.iter (fun staleDiaryId -> updateNoteSummary conn staleDiaryId userId)

let noteSummary (conn: NpgsqlConnection) (note: Diary) =
    let noteId = note.NoteId

    let existingSummaryUpdatedAt =
        [ SummaryRepository.lastUpdated conn note.UserId noteId ]

    match existingSummaryUpdatedAt with
    | [] ->
        let summary = summaryJson note
        SummaryRepository.insertOrUpdate conn noteId note.UserId summary
        summary
    | _ :: _ ->
        if DiaryRepository.isSummaryStale conn note.UserId noteId then
            let summary = summaryJson note
            SummaryRepository.insertOrUpdate conn noteId note.UserId summary
            summary
        else
            let summary = SummaryRepository.getByUserAndNoteId conn note.UserId noteId
            summary.Content

let freqsOfNote (conn: NpgsqlConnection) (note: Diary) =
    { Id = note.Id
      NoteId = note.NoteId
      Note = note |> noteSummary conn
      UserId = note.UserId
      SearchText = note.SearchText
      SearchTerms = note.SearchTerms
      LastUpdated = note.LastUpdated }
