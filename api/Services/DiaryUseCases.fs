module DiaryUseCases

open Database
open ApplicationContracts

type QueueBackedBackgroundJobPublisher(summaryQueue: SummaryQueue.SummaryUpdateQueue, indexQueue: IndexQueue.IndexUpdateQueue) =
    interface IBackgroundJobPublisher with
        member _.EnqueueSummaryUpdate(noteRef: NoteRef) =
            summaryQueue.Enqueue(noteRef.UserId, noteRef.NoteId) |> ignore

        member _.EnqueueIndexUpdate(noteRef: NoteRef) =
            indexQueue.Enqueue(noteRef.UserId, noteRef.NoteId) |> ignore

type DiaryWriteUseCase(db: DbSession, publisher: IBackgroundJobPublisher) =
    interface IDiaryWriteUseCase with
        member _.SaveDiary(userId: int, note: Diary) =
            DiaryService.saveDiary db publisher userId note
