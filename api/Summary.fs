module Jieba

open System.Text
open JiebaNet

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




let insertSummary (note: Diary) =
    let summaryJson = note.Note |> TipTap.getTextFromNote |> freqs |> Json.Convert.toJson
    // store as jsonb
    summaryJson

let updateNoteSummary conn (note_id: string) (user_id: int) =
    let summary =
        Diary.DiaryByUserIDAndID conn { NoteId = note_id; UserId = user_id }
        |> _.Note
        |> TipTap.getTextFromNote
        |> freqs
        |> Json.Convert.toJson

    Summary.InsertSummary
        conn
        { NoteId = note_id
          UserId = user_id
          Content = summary }
    |> ignore


let refreshSummary conn user_id =
    let staledIds =
        Diary.GetStaleIdsOfUserId conn user_id |> List.map (fun x -> x.NoteId)

    staledIds
    |> List.iter (fun stale_diary_id -> updateNoteSummary conn stale_diary_id user_id)

let noteSummary conn (note: Diary) =
    // get summary directly from note
    // check summary is exits
    let nid = note.NoteId
    printfn "%s" nid
    let updateP: Summary.LastUpdatedParams = { NoteId = nid; UserId = note.UserId }

    let noteDt = [ Summary.LastUpdated conn updateP ]

    match noteDt with
    | [] ->
        let summary = note.Note |> TipTap.getTextFromNote |> freqs |> Json.Convert.toJson
        // insert new item
        ignore (
            Summary.InsertSummary
                conn
                { NoteId = nid
                  Content = summary
                  UserId = note.UserId }
        )

        summary
    | head :: _ ->
        let staled = Diary.CheckIdStale conn { NoteId = nid; UserId = note.UserId }

        if staled then
            let summary = note.Note |> TipTap.getTextFromNote |> freqs |> Json.Convert.toJson
            // update the summary and timestamp
            ignore (
                Summary.InsertSummary
                    conn
                    { NoteId = nid
                      Content = summary
                      UserId = note.UserId }
            )

            summary
        else
            let summary =
                Summary.GetSummaryByUserIDAndID conn { NoteId = nid; UserId = note.UserId }

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
      NoteId = note.NoteId
      Note = note |> noteSummary conn
      UserId = note.UserId
      LastUpdated = note.LastUpdated }
