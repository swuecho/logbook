module Util

open System
open System.Text
open System.Threading.Tasks

/// Runs a synchronous action with a timeout on a background thread.
/// Fails fast with a clear message if the action does not complete within the given duration.
let runWithTimeout (timeout: TimeSpan) (name: string) (action: unit -> 'T) : 'T =
    let task = Task.Run(action)

    if task.Wait(timeout) then
        try
            task.Result
        with :? AggregateException as agg when agg.InnerExceptions.Count = 1 ->
            raise agg.InnerExceptions[0]
    else
        failwithf "Startup timed out after %.0fs while %s. Is the database reachable?" timeout.TotalSeconds name

let getEnvVar varName =
    Environment.GetEnvironmentVariable(varName)

let requiredEnvVar varName =
    match getEnvVar varName with
    | value when String.IsNullOrWhiteSpace(value) ->
        invalidOp $"Missing required environment variable: {varName}"
    | value -> value

/// Join a sequence of strings using a delimiter.
/// Equivalent to String.Join() but without arrays.
let join (delim: string) (items: seq<string>) =
    // Collect the result in the string builder buffer
    // The end-sequence will be "item1,delim,...itemN,delim"
    let buff =
        Seq.fold (fun (buff: StringBuilder) (s: string) -> buff.Append(s).Append(delim)) (StringBuilder()) items

    buff.Remove(buff.Length - delim.Length, delim.Length).ToString()
