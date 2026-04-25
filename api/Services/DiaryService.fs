module DiaryService

open System
open System.Text.RegularExpressions
open Npgsql

type DiarySearchResult =
    { NoteId: string
      Snippet: string
      Rank: int
      LastUpdated: DateTime }

let compactText (text: string) =
    Regex.Replace(text, @"\s+", " ").Trim()

let buildSnippet (terms: string array) (searchText: string) =
    let text = compactText searchText

    if text.Length <= 180 then
        text
    else
        let lowerText = text.ToLowerInvariant()

        let firstMatch =
            terms
            |> Array.choose (fun term ->
                let idx = lowerText.IndexOf(term, StringComparison.Ordinal)
                if idx >= 0 then Some idx else None)
            |> Array.sort
            |> Array.tryHead

        let center = defaultArg firstMatch 0
        let start = max 0 (center - 60)
        let length = min (text.Length - start) 180
        let snippet = text.Substring(start, length)

        let prefix = if start > 0 then "..." else ""
        let suffix = if start + length < text.Length then "..." else ""
        prefix + snippet + suffix

let listSummaries (conn: NpgsqlConnection) userId =
    SearchService.refreshSummary conn userId

    Summary.GetSummaryByUserId conn userId
    |> List.filter (fun x -> x.Note.Length > 2)

let getOrCreateDiary (conn: NpgsqlConnection) userId noteId =
    try
        Diary.DiaryByUserIDAndID conn { NoteId = noteId; UserId = userId }
    with :? NoResultsException ->
        Diary.AddNote
            conn
            { NoteId = noteId
              UserId = userId
              Note = "" }

let saveDiary (conn: NpgsqlConnection) userId (note: Diary) =
    let saved =
        Diary.AddNote
            conn
            { NoteId = note.NoteId
              UserId = userId
              Note = note.Note }

    SearchService.updateSearchIndex conn saved.NoteId saved.UserId saved.Note
    saved

let search (conn: NpgsqlConnection) userId query =
    let terms = SearchService.searchTerms query

    if terms.Length = 0 then
        []
    else
        Diary.SearchDiary conn { UserId = userId; QueryTerms = terms }
        |> List.map (fun row ->
            { NoteId = row.NoteId
              Snippet = buildSnippet terms row.SearchText
              Rank = row.Rank
              LastUpdated = row.LastUpdated })

let extractTodoLists allDiary =
    allDiary
    |> List.choose (fun diary ->
        let todoList = TipTap.extractTodoList diary.Note

        if List.isEmpty todoList then
            None
        else
            Some
                {| noteId = diary.NoteId
                   todoList = todoList |})

let todoDocument (conn: NpgsqlConnection) userId =
    Diary.ListDiaryByUserID conn userId
    |> extractTodoLists
    |> TipTap.constructTipTapDoc

let listDiaryIds (conn: NpgsqlConnection) userId =
    Diary.ListDiaryIDByUserID conn userId
