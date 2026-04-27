module DiaryService

open System
open System.Text.RegularExpressions
open Database

type DiarySearchResult =
    { NoteId: string
      Snippet: string
      Rank: int
      LastUpdated: DateTime }

let private maxSnippetLength = 180
let private snippetContextRadius = 60
let private minSummaryLength = 2

let compactText (text: string) =
    Regex.Replace(text, @"\s+", " ").Trim()

let buildSnippet (terms: string array) (searchText: string) =
    let text = compactText searchText

    if text.Length <= maxSnippetLength then
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
        let start = max 0 (center - snippetContextRadius)
        let length = min (text.Length - start) maxSnippetLength
        let snippet = text.Substring(start, length)

        let prefix = if start > 0 then "..." else ""
        let suffix = if start + length < text.Length then "..." else ""
        prefix + snippet + suffix

let private hasVisibleSummaryText (summary: Summary.GetSummaryByUserIdRow) =
    summary.Note.Length > minSummaryLength

let private toSearchResult terms (row: Diary.SearchDiaryRow) =
    { NoteId = row.NoteId
      Snippet = buildSnippet terms row.SearchText
      Rank = row.Rank
      LastUpdated = row.LastUpdated }

let listSummaries (db: DbSession) userId =
    db.WithConnection(fun conn ->
        SummaryRepository.getByUserId conn userId
        |> List.filter hasVisibleSummaryText)

let getOrCreateDiary (db: DbSession) userId noteId =
    db.WithConnection(fun conn ->
        try
            DiaryRepository.getByUserAndNoteId conn userId noteId
        with :? NoResultsException ->
            DiaryRepository.addOrUpdate conn noteId userId "")

let private hasSearchIndex (diary: Diary) =
    String.IsNullOrEmpty diary.Note
    || not (String.IsNullOrEmpty diary.SearchText)
    || diary.SearchTerms.Length > 0

let saveDiary
    (db: DbSession)
    (summaryQueue: SummaryBackgroundService.SummaryUpdateQueue)
    (todoCache: TodoCacheService.TodoDocumentCache)
    userId
    (note: Diary)
    =
    let saved, changed, affectsTodo =
        db.WithTransaction(fun conn ->
            let existing =
                try
                    Some(DiaryRepository.getByUserAndNoteId conn userId note.NoteId)
                with :? NoResultsException ->
                    None

            let affectsTodo =
                TipTap.containsTodoNodeMarker note.Note
                || (existing |> Option.exists (fun diary -> TipTap.containsTodoNodeMarker diary.Note))

            match existing with
            | Some diary when diary.Note = note.Note && hasSearchIndex diary ->
                diary, false, affectsTodo
            | _ ->
                let saved = DiaryRepository.addOrUpdate conn note.NoteId userId note.Note
                let searchText, searchTerms = SearchIndexService.updateSearchIndex conn saved.NoteId saved.UserId saved.Note

                { saved with
                    SearchText = searchText
                    SearchTerms = searchTerms },
                true,
                affectsTodo)

    if changed then
        summaryQueue.Enqueue(saved.UserId, saved.NoteId) |> ignore

    if changed && affectsTodo then
        todoCache.Invalidate(saved.UserId)

    saved

let search (db: DbSession) userId query =
    let terms = TextAnalysis.searchTerms query

    if terms.Length = 0 then
        []
    else
        db.WithConnection(fun conn ->
            DiaryRepository.search conn userId terms
            |> List.map (toSearchResult terms))

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

let private buildTodoDocument (db: DbSession) userId =
    db.WithConnection(fun conn ->
        DiaryRepository.listWithTodoByUserId conn userId
        |> extractTodoLists
        |> TipTap.constructTipTapDoc)

let todoDocument (db: DbSession) (todoCache: TodoCacheService.TodoDocumentCache) userId =
    todoCache.GetOrCreate(userId, fun () -> buildTodoDocument db userId)

let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listIdsByUserId conn userId)
