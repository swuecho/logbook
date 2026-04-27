module SummaryBackgroundService

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Npgsql

type SummaryUpdateRequest = { UserId: int; NoteId: string }

// A lightweight in-memory queue for summary work.
//
// Saving a diary note used to rebuild the summary inside the request. That made
// saves slower because summary generation parses TipTap JSON and runs text
// analysis. Now the request only enqueues the note ID after the diary save
// commits, and this background worker updates the summary shortly after.
//
// This queue is intentionally in-process. If the app stops after a save but
// before the queued item is processed, the item is lost. The periodic stale
// summary sweep below is the recovery mechanism for that case.
type SummaryUpdateQueue() =
    let options = UnboundedChannelOptions(SingleReader = true, SingleWriter = false)
    let channel = Channel.CreateUnbounded<string>(options)
    let pending = ConcurrentDictionary<string, SummaryUpdateRequest>()

    let keyOf userId noteId = $"{userId}:{noteId}"

    member _.Enqueue(userId, noteId) =
        let key = keyOf userId noteId
        let request = { UserId = userId; NoteId = noteId }

        let added =
            pending.TryAdd(key, request)

        if not added then
            pending[key] <- request
            true
        else
            channel.Writer.TryWrite(key)

    member _.Reader = channel.Reader

    member _.TryTake(key: string) =
        match pending.TryRemove(key) with
        | true, request -> Some request
        | false, _ -> None

type SummaryRefreshWorker(dataSource: NpgsqlDataSource, queue: SummaryUpdateQueue) =
    inherit BackgroundService()

    // The sweep is a fallback, not the normal path. Most summaries should be
    // updated by the queue within a moment of saving. The sweep repairs missed
    // queue items and any summaries that became stale for another reason.
    let refreshInterval = TimeSpan.FromMinutes(10.0)

    // Use a fresh connection for each background operation. DbSession is scoped
    // to HTTP-style usage elsewhere, while the hosted service runs outside a
    // request context and can safely open connections from the shared data source.
    let updateOne (request: SummaryUpdateRequest) =
        use conn = dataSource.OpenConnection()
        SummaryService.updateNoteSummary conn request.NoteId request.UserId

    let refreshAll () =
        use conn = dataSource.OpenConnection()
        SummaryService.refreshAllSummaries conn

    // Main fast path: consume note IDs from the queue one at a time and rebuild
    // their summaries. Requests are keyed by user/note and held briefly so a
    // typing burst produces one summary refresh for the final saved content.
    // A single reader keeps CPU-heavy text analysis from running concurrently.
    //
    // Individual failures are logged and swallowed so one bad note cannot stop
    // future queued summary updates. The periodic sweep will retry stale notes.
    let runQueueLoop (stoppingToken: CancellationToken) =
        task {
            try
                while not stoppingToken.IsCancellationRequested do
                    let! key = queue.Reader.ReadAsync(stoppingToken).AsTask()
                    do! Task.Delay(TimeSpan.FromSeconds(1.0), stoppingToken)

                    match queue.TryTake key with
                    | None -> ()
                    | Some request ->
                        try
                            updateOne request
                        with ex ->
                            printfn "Failed to update diary summary for user %d note %s: %O" request.UserId request.NoteId ex
            with :? OperationCanceledException ->
                ()
        }

    // Recovery path: periodically scan for any diary row whose summary is
    // missing or older than the diary. This keeps summaries eventually
    // consistent even if the process crashed before processing an enqueued item.
    let runRefreshLoop (stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(refreshInterval)

            try
                while! timer.WaitForNextTickAsync(stoppingToken).AsTask() do
                    try
                        refreshAll ()
                    with ex ->
                        printfn "Failed to refresh stale diary summaries: %O" ex
            with :? OperationCanceledException ->
                ()
        }

    // Run the queue consumer and recovery sweep for the lifetime of the app.
    // ASP.NET Core passes a cancellation token during shutdown; both loops catch
    // that cancellation so normal shutdown does not log as an error.
    override _.ExecuteAsync(stoppingToken: CancellationToken) : Task =
        Task.WhenAll(runQueueLoop stoppingToken, runRefreshLoop stoppingToken)
