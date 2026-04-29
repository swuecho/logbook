module IndexQueue

open System.Collections.Concurrent
open System.Threading.Channels

type IndexUpdateRequest = { UserId: int; NoteId: string }

type IndexUpdateQueue() =
    let options = UnboundedChannelOptions(SingleReader = true, SingleWriter = false)
    let channel = Channel.CreateUnbounded<string>(options)
    let pending = ConcurrentDictionary<string, IndexUpdateRequest>()

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

