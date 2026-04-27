module SummaryService

open Npgsql

let summaryJson (note: Diary) =
    note.Note |> TipTap.getTextFromNote |> TextAnalysis.freqs |> Json.Convert.toJson

let updateSummaryForNote (conn: NpgsqlConnection) (note: Diary) =
    SummaryRepository.insertOrUpdate conn note.NoteId note.UserId (summaryJson note)

let updateNoteSummary (conn: NpgsqlConnection) noteId userId =
    let summary =
        DiaryRepository.getByUserAndNoteId conn userId noteId
        |> summaryJson

    SummaryRepository.insertOrUpdate conn noteId userId summary

let refreshSummary (conn: NpgsqlConnection) userId =
    DiaryRepository.listStaleSummaryIdsByUserId conn userId
    |> List.map (fun x -> x.NoteId)
    |> List.iter (fun staleDiaryId -> updateNoteSummary conn staleDiaryId userId)

let refreshAllSummaries (conn: NpgsqlConnection) =
    DiaryRepository.listStaleSummaryIds conn
    |> List.iter (fun diary -> updateSummaryForNote conn diary)

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
