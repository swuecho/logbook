module BackgroundJobsService

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open ApplicationContracts

/// Single hosted service that runs:
/// - summary queue + periodic stale sweep
/// - index/todo queue + periodic recovery sweep
///
/// Keeps the request path light while avoiding multiple hosted services.
type Worker
    (
        maintenanceService: IBackgroundMaintenanceService,
        summaryQueue: SummaryQueue.SummaryUpdateQueue,
        indexQueue: IndexQueue.IndexUpdateQueue,
        logger: ILogger<Worker>
    ) =
    inherit BackgroundService()

    let refreshInterval = TimeSpan.FromMinutes(10.0)
    let debounceSummary = TimeSpan.FromMilliseconds(500.0)
    let debounceIndex = TimeSpan.FromMilliseconds(200.0)

    // Fixed throttling to cap background throughput / CPU usage.
    let throttlePerItem = TimeSpan.FromMilliseconds(25.0)

    let toNoteRef userId noteId = { UserId = userId; NoteId = noteId }

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
                            maintenanceService.UpdateSummary(toNoteRef request.UserId request.NoteId)
                            do! Task.Delay(throttlePerItem, stoppingToken)
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
                            maintenanceService.UpdateIndexAndTodo(toNoteRef request.UserId request.NoteId)
                            do! Task.Delay(throttlePerItem, stoppingToken)
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
                        maintenanceService.RefreshSummaries()
                        logger.LogInformation("Summary sweep finished")
                    with ex ->
                        logger.LogWarning(ex, "Failed to refresh stale summaries")
            with :? OperationCanceledException ->
                ()
        }

    let runIndexSweepLoop (stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(refreshInterval * 2.0)

            try
                while! timer.WaitForNextTickAsync(stoppingToken).AsTask() do
                    try
                        logger.LogInformation("Index/todo sweep starting")
                        maintenanceService.RefreshIndexAndTodo()
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

