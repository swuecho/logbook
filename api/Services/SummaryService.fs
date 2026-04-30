module SummaryService

open Database
open Npgsql

let summaryJson (note: Diary) =
    note.Note |> TipTap.getTextFromNote |> TextAnalysis.freqs |> Json.Convert.toJson

// ── Private helpers that share an open NpgsqlConnection ──────────────

let private updateSummaryForNote (conn: NpgsqlConnection) (note: Diary) =
    SummaryRepository.insertOrUpdate conn note.NoteId note.UserId (summaryJson note)

let private updateNoteSummaryOnConn (conn: NpgsqlConnection) noteId userId =
    let summary =
        DiaryRepository.getByUserAndNoteId conn userId noteId
        |> summaryJson

    SummaryRepository.insertOrUpdate conn noteId userId summary

let private noteSummaryOnConn (conn: NpgsqlConnection) (note: Diary) =
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

// ── Public API (DbSession) ───────────────────────────────────────────

let updateNoteSummary (db: DbSession) noteId userId =
    db.WithConnection(fun conn -> updateNoteSummaryOnConn conn noteId userId)

let refreshAllSummaries (db: DbSession) =
    db.WithConnection(fun conn ->
        DiaryRepository.listStaleSummaryIds conn
        |> List.iter (fun diary -> updateSummaryForNote conn diary))

let refreshSummary (db: DbSession) userId =
    db.WithConnection(fun conn ->
        DiaryRepository.listStaleSummaryIdsByUserId conn userId
        |> List.map (fun x -> x.NoteId)
        |> List.iter (fun staleDiaryId -> updateNoteSummaryOnConn conn staleDiaryId userId))

let noteSummary (db: DbSession) (note: Diary) =
    db.WithConnection(fun conn -> noteSummaryOnConn conn note)

let freqsOfNote (db: DbSession) (note: Diary) =
    db.WithConnection(fun conn ->
        { Id = note.Id
          NoteId = note.NoteId
          Note = note |> noteSummaryOnConn conn
          UserId = note.UserId
          SearchText = note.SearchText
          SearchTerms = note.SearchTerms
          LastUpdated = note.LastUpdated })
