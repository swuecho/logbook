module Jieba

open System.Text
open JiebaNet
open FSharp.Data

let freqs text =
    let wordRegex = RegularExpressions.Regex "\\w"
    let resourceDir = __SOURCE_DIRECTORY__ + "/Resources/jieba"

    Segmenter.ConfigManager.ConfigFileBaseDir <- resourceDir

    let seg = Segmenter.JiebaSegmenter()
    let words = seg.Cut(text)

    words
    |> Seq.countBy id
    |> Seq.sortBy (snd >> (~-))
    |> Seq.truncate 40
    |> Seq.filter (fun (a, b) -> wordRegex.IsMatch a)
    |> dict

/// Join a sequence of strings using a delimiter.
/// Equivalent to String.Join() but without arrays.
let join (delim: string) (items: seq<string>) =
    // Collect the result in the string builder buffer
    // The end-sequence will be "item1,delim,...itemN,delim"
    let buff =
        Seq.fold (fun (buff: StringBuilder) (s: string) -> buff.Append(s).Append(delim)) (StringBuilder()) items

    buff.Remove(buff.Length - delim.Length, delim.Length).ToString()

let rec getContent (jv: JsonValue) =
    let extractProperty (x, y) =
        match (x, y) with
        | ("content", y) -> getContent y
        | ("text", y) -> string y
        | (_, y) -> " "

    match jv with
    // JsonValue.Array(elements) -> Array.map getContent elements
    | JsonValue.Array(arr) -> arr |> Array.map getContent |> join " "
    | JsonValue.Record(record) -> record |> Array.map extractProperty |> join " "
    | JsonValue.String(str) -> str
    | JsonValue.Number(n) -> n |> string
    | JsonValue.Float(f) -> f |> string
    | JsonValue.Boolean(b) -> b |> string
    | JsonValue.Null -> ""

let getTextFromNote (note: Diary) =
    let content = sprintf "%s" note.Note

    match JsonValue.TryParse content with
    | Some(json) -> getContent json
    | None -> content

let insertSummary (note: Diary) =
    let summaryJson = note |> getTextFromNote |> freqs |> Json.Convert.toJson
    // store as jsonb
    summaryJson

let updateNoteSummary conn (id: string) (user_id: int) =
    let summary =
        Diary.DiaryByUserIDAndID conn { Id = id; UserId = user_id }
        |> getTextFromNote
        |> freqs
        |> Json.Convert.toJson

    NoteQ.insertSummary id user_id summary |> ignore


let refreshSummary conn user_id =
    let staledIds = Diary.GetStaleIdsOfUserId conn user_id |> List.map (fun x -> x.Id)
    printfn "%A" staledIds

    staledIds
    |> List.iter (fun stale_diary_id -> updateNoteSummary conn stale_diary_id user_id)

let noteSummary conn (note: Diary) =
    // get summary directly from note
    // check summary is exits
    let nid = note.Id
    printfn "%s" nid
    let noteDt = NoteQ.getSummaryLastUpdated nid note.UserId

    match noteDt with
    | [] ->
        let summary = note |> getTextFromNote |> freqs |> Json.Convert.toJson
        // insert new item
        ignore (NoteQ.insertSummary nid note.UserId summary)
        summary
    | head :: _ ->
        let staled = NoteQ.checkIdStale nid note.UserId

        if staled then
            let summary = note |> getTextFromNote |> freqs |> Json.Convert.toJson
            // update the summary and timestamp
            ignore (NoteQ.insertSummary nid note.UserId summary)
            summary
        else
            let summary =
                Summary.GetSummaryByUserIDAndID conn { Id = nid; UserId = note.UserId }

            summary.Content
// fetch summary directly

// check if stale
// checkIfStale nid head



//printfn "%s" (myDate.ToString
// select last_updated from summary
// summary not exits  -> insert
//      exits but stale ( summary.lastmodified < note.lastmodified ) ->  update
//      exists and up to date -> return the summary content (this branch is majority)

// note |> getTextFromNote |> freqs

let freqsOfNote conn (note: Diary) =
    { Id = note.Id
      Note = note |> noteSummary conn
      UserId = note.UserId
      LastUpdated = note.LastUpdated

    }
