module DiaryService

open System
open System.Text.RegularExpressions
open Database
open ApplicationContracts

type DiarySearchResult =
    { NoteId: string
      Snippet: string
      Rank: int
      LastUpdated: DateTime }

type QueueBackedBackgroundJobPublisher(summaryQueue: SummaryQueue.SummaryUpdateQueue, indexQueue: IndexQueue.IndexUpdateQueue) =
    interface IBackgroundJobPublisher with
        member _.EnqueueSummaryUpdate(noteRef: NoteRef) =
            summaryQueue.Enqueue(noteRef.UserId, noteRef.NoteId) |> ignore

        member _.EnqueueIndexUpdate(noteRef: NoteRef) =
            indexQueue.Enqueue(noteRef.UserId, noteRef.NoteId) |> ignore

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
        match DiaryRepository.tryGetByUserAndNoteId conn userId noteId with
        | Some diary -> diary
        | None -> DiaryRepository.addOrUpdate conn noteId userId "")

let private hasSearchIndex (diary: Diary) =
    String.IsNullOrEmpty diary.Note
    || not (String.IsNullOrEmpty diary.SearchText)
    || diary.SearchTerms.Length > 0

let private updateTodoForNote conn (diary: Diary) =
    match TipTap.extractTodoListJson diary.Note with
    | None -> TodoRepository.delete conn diary.NoteId diary.UserId
    | Some todosJson -> TodoRepository.insertOrUpdate conn diary.NoteId diary.UserId todosJson

let saveDiary
    (db: DbSession)
    (publisher: IBackgroundJobPublisher)
    userId
    (note: Diary)
    =
    let normalizedNote =
        if TipTap.isEffectivelyEmpty note.Note then
            ""
        else
            note.Note

    let saved, changed =
        db.WithTransaction(fun conn ->
            let existing = DiaryRepository.tryGetByUserAndNoteId conn userId note.NoteId

            match existing with
            | Some diary when diary.Note = normalizedNote && hasSearchIndex diary ->
                diary, false
            | _ ->
                let saved = DiaryRepository.addOrUpdate conn note.NoteId userId normalizedNote
                // Search indexing + todo extraction can be CPU-heavy; run them async in background.
                // The periodic sweep will repair if the queue misses an item (e.g. process restart).
                saved, true)

    if changed then
        let noteRef = { UserId = saved.UserId; NoteId = saved.NoteId }
        publisher.EnqueueSummaryUpdate noteRef
        publisher.EnqueueIndexUpdate noteRef

    saved

let search (db: DbSession) userId query =
    let terms = TextAnalysis.searchTerms query

    if terms.Length = 0 then
        []
    else
        db.WithConnection(fun conn ->
            DiaryRepository.search conn userId terms
            |> List.map (toSearchResult terms))

let precomputeTodoRows (conn: Npgsql.NpgsqlConnection) =
    DiaryRepository.listWithTodo conn
    |> List.iter (updateTodoForNote conn)

let private todoRowsToDocument rows =
    rows
    |> List.choose (fun (row: Todo.GetTodoByUserIdRow) ->
        let todoList = TipTap.deserializeTodoList row.Todos

        if List.isEmpty todoList then
            None
        else
            Some
                {| noteId = row.NoteId
                   todoList = todoList |})
    |> TipTap.constructTipTapDoc

let todoDocument (db: DbSession) userId =
    db.WithConnection(fun conn ->
        TodoRepository.getByUserId conn userId
        |> todoRowsToDocument)

let listDiaryIds (db: DbSession) userId =
    db.WithConnection(fun conn -> DiaryRepository.listIdsByUserId conn userId)
