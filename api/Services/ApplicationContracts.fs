module ApplicationContracts

type NoteRef = { UserId: int; NoteId: string }

type IBackgroundJobPublisher =
    abstract member EnqueueSummaryUpdate: noteRef: NoteRef -> unit
    abstract member EnqueueIndexUpdate: noteRef: NoteRef -> unit

type IBackgroundMaintenanceService =
    abstract member UpdateSummary: noteRef: NoteRef -> unit
    abstract member RefreshSummaries: unit -> unit
    abstract member UpdateIndexAndTodo: noteRef: NoteRef -> unit
    abstract member RefreshIndexAndTodo: unit -> unit
