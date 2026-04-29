module SummaryQueue

open System.Collections.Concurrent
open System.Threading.Channels

type SummaryUpdateRequest = { UserId: int; NoteId: string }

/// A lightweight in-memory queue for summary work.
///
/// The queue is intentionally in-process: if the app stops after enqueue but
/// before processing, that item is lost. A periodic "stale summary" sweep in
/// the hosted background worker is the recovery mechanism.
type SummaryUpdateQueue() =
    let options = UnboundedChannelOptions(SingleReader = true, SingleWriter = false)
    let channel = Channel.CreateUnbounded<string>(options)
    let pending = ConcurrentDictionary<string, SummaryUpdateRequest>()

    let keyOf userId noteId = $"{userId}:{noteId}"

    member _.Enqueue(userId, noteId) =
        let key = keyOf userId noteId
        let request = { UserId = userId; NoteId = noteId }

        let added = pending.TryAdd(key, request)

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

