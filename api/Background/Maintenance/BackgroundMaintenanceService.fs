module BackgroundMaintenanceService

open Database
open ApplicationContracts
open Npgsql

let private updateSearchIndex (conn: NpgsqlConnection) noteId userId note =
    let searchText, terms = TextAnalysis.searchIndexOfNote note
    DiaryRepository.updateSearchIndex conn noteId userId searchText terms
    searchText, terms

let private refreshSearchIndex (conn: NpgsqlConnection) =
    DiaryRepository.listMissingSearchIndex conn
    |> List.iter (fun diary -> updateSearchIndex conn diary.NoteId diary.UserId diary.Note |> ignore)

type BackgroundMaintenanceService(db: DbSession) =
    interface IBackgroundMaintenanceService with
        member _.UpdateSummary(noteRef: NoteRef) =
            db.WithConnection(fun conn -> SummaryService.updateNoteSummary conn noteRef.NoteId noteRef.UserId)

        member _.RefreshSummaries() =
            db.WithConnection SummaryService.refreshAllSummaries

        member _.UpdateIndexAndTodo(noteRef: NoteRef) =
            db.WithConnection(fun conn ->
                let diary = DiaryRepository.getByUserAndNoteId conn noteRef.UserId noteRef.NoteId
                updateSearchIndex conn diary.NoteId diary.UserId diary.Note |> ignore

                if not (TipTap.containsTodoNodeMarker diary.Note) then
                    TodoRepository.delete conn diary.NoteId diary.UserId
                else
                    match TipTap.extractTodoListJson diary.Note with
                    | None -> TodoRepository.delete conn diary.NoteId diary.UserId
                    | Some todosJson -> TodoRepository.insertOrUpdate conn diary.NoteId diary.UserId todosJson)

        member _.RefreshIndexAndTodo() =
            db.WithConnection(fun conn ->
                refreshSearchIndex conn
                DiaryService.precomputeTodoRows conn
                TodoRepository.deleteStaleTodos conn)

