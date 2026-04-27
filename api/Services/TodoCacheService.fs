module TodoCacheService

open System
open System.Collections.Concurrent
open System.Threading

type TodoDocumentCache() =
    let cache = ConcurrentDictionary<int, Lazy<TipTap.TipTapDocument>>()

    member _.GetOrCreate(userId: int, factory: unit -> TipTap.TipTapDocument) =
        let lazyDocument =
            cache.GetOrAdd(
                userId,
                fun _ -> Lazy<TipTap.TipTapDocument>(Func<TipTap.TipTapDocument>(factory), LazyThreadSafetyMode.ExecutionAndPublication)
            )

        try
            lazyDocument.Value
        with _ ->
            cache.TryRemove userId |> ignore
            reraise ()

    member _.Invalidate(userId: int) =
        cache.TryRemove userId |> ignore
