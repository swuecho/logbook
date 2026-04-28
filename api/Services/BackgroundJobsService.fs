module BackgroundJobsService

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Npgsql

/// Single hosted service that runs:
/// - summary queue + periodic stale sweep
/// - index/todo queue + periodic recovery sweep
///
/// Keeps the request path light while avoiding multiple hosted services.
type Worker
    (
        dataSource: NpgsqlDataSource,
        summaryQueue: SummaryQueue.SummaryUpdateQueue,
        indexQueue: IndexQueue.IndexUpdateQueue,
        logger: ILogger<Worker>
    ) =
    inherit BackgroundService()

    let refreshInterval = TimeSpan.FromMinutes(10.0)
    let debounceSummary = TimeSpan.FromMilliseconds(500.0)
    let debounceIndex = TimeSpan.FromMilliseconds(200.0)

    let updateSummary (request: SummaryQueue.SummaryUpdateRequest) =
        use conn = dataSource.OpenConnection()
        SummaryService.updateNoteSummary conn request.NoteId request.UserId

    let refreshSummaries () =
        use conn = dataSource.OpenConnection()
        SummaryService.refreshAllSummaries conn

    let updateIndexAndTodo (request: IndexQueue.IndexUpdateRequest) =
        use conn = dataSource.OpenConnection()

        let diary = DiaryRepository.getByUserAndNoteId conn request.UserId request.NoteId

        // Search index
        SearchIndexService.updateSearchIndex conn diary.NoteId diary.UserId diary.Note |> ignore

        // Todo extraction
        if not (TipTap.containsTodoNodeMarker diary.Note) then
            TodoRepository.delete conn diary.NoteId diary.UserId
        else
            match TipTap.extractTodoListJson diary.Note with
            | None -> TodoRepository.delete conn diary.NoteId diary.UserId
            | Some todosJson -> TodoRepository.insertOrUpdate conn diary.NoteId diary.UserId todosJson

    let refreshIndexAndTodo () =
        use conn = dataSource.OpenConnection()
        SearchIndexService.refreshSearchIndex conn
        DiaryService.precomputeTodoRows conn

    let runSummaryQueueLoop (stoppingToken: CancellationToken) =
        task {
            try
                while not stoppingToken.IsCancellationRequested do
                    let! key = summaryQueue.Reader.ReadAsync(stoppingToken).AsTask()
                    do! Task.Delay(debounceSummary, stoppingToken)

                    match summaryQueue.TryTake key with
                    | None -> ()
                    | Some request ->
                        try
                            updateSummary request
                        with ex ->
                            logger.LogWarning(
                                ex,
                                "Failed to update summary for user {UserId} note {NoteId}",
                                request.UserId,
                                request.NoteId
                            )
            with :? OperationCanceledException ->
                ()
        }

    let runIndexQueueLoop (stoppingToken: CancellationToken) =
        task {
            try
                while not stoppingToken.IsCancellationRequested do
                    let! key = indexQueue.Reader.ReadAsync(stoppingToken).AsTask()
                    do! Task.Delay(debounceIndex, stoppingToken)

                    match indexQueue.TryTake key with
                    | None -> ()
                    | Some request ->
                        try
                            updateIndexAndTodo request
                        with ex ->
                            logger.LogWarning(
                                ex,
                                "Failed to update index/todos for user {UserId} note {NoteId}",
                                request.UserId,
                                request.NoteId
                            )
            with :? OperationCanceledException ->
                ()
        }

    let runSummarySweepLoop (stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(refreshInterval)

            try
                while! timer.WaitForNextTickAsync(stoppingToken).AsTask() do
                    try
                        logger.LogInformation("Summary sweep starting")
                        refreshSummaries ()
                        logger.LogInformation("Summary sweep finished")
                    with ex ->
                        logger.LogWarning(ex, "Failed to refresh stale summaries")
            with :? OperationCanceledException ->
                ()
        }

    let runIndexSweepLoop (stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(refreshInterval)

            try
                while! timer.WaitForNextTickAsync(stoppingToken).AsTask() do
                    try
                        logger.LogInformation("Index/todo sweep starting")
                        refreshIndexAndTodo ()
                        logger.LogInformation("Index/todo sweep finished")
                    with ex ->
                        logger.LogWarning(ex, "Failed to refresh missing search index / todo rows")
            with :? OperationCanceledException ->
                ()
        }

    override _.ExecuteAsync(stoppingToken: CancellationToken) : Task =
        Task.WhenAll(
            runSummaryQueueLoop stoppingToken,
            runIndexQueueLoop stoppingToken,
            runSummarySweepLoop stoppingToken,
            runIndexSweepLoop stoppingToken
        )

