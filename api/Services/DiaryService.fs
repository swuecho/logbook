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

let saveDiary (db: DbSession) userId (note: Diary) =
    db.WithTransaction(fun conn ->
        let saved = DiaryRepository.addOrUpdate conn note.NoteId userId note.Note
        let searchText, searchTerms = SearchIndexService.updateSearchIndex conn saved.NoteId saved.UserId saved.Note
        SummaryService.updateSummaryForNote conn saved

        { saved with
            SearchText = searchText
            SearchTerms = searchTerms })

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

let todoDocument (db: DbSession) userId =
    db.WithConnection(fun conn ->
        DiaryRepository.listByUserId conn userId
        |> extractTodoLists
        |> TipTap.constructTipTapDoc)

let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listIdsByUserId conn userId)
