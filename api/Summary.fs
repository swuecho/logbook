module Jieba

open System
open System.Text.RegularExpressions
open JiebaNet

let private resourceDir = __SOURCE_DIRECTORY__ + "/Resources/jieba"
let private searchableToken = Regex(@"[\p{L}\p{N}]", RegexOptions.Compiled)

let private segment (text: string) =
    Segmenter.ConfigManager.ConfigFileBaseDir <- resourceDir
    let seg = Segmenter.JiebaSegmenter()
    seg.Cut(text)

let searchTerms (text: string) =
    if String.IsNullOrWhiteSpace text then
        [||]
    else
        text
        |> segment
        |> Seq.map (fun word -> word.Trim().ToLowerInvariant())
        |> Seq.filter (fun word -> word.Length > 0 && searchableToken.IsMatch word)
        |> Seq.distinct
        |> Seq.toArray

let searchIndexOfNote (note: string) =
    let searchText = note |> TipTap.getTextFromNote
    searchText, searchTerms searchText

let updateSearchIndex conn (noteId: string) (userId: int) (note: string) =
    let searchText, terms = searchIndexOfNote note

    Diary.UpdateDiarySearch
        conn
        { NoteId = noteId
          UserId = userId
          SearchText = searchText
          SearchTerms = terms }
    |> ignore

let refreshSearchIndex conn =
    Diary.ListMissingSearchIndex conn
    |> List.iter (fun diary -> updateSearchIndex conn diary.NoteId diary.UserId diary.Note)

let freqs text =
    let words = segment text

    words
    |> Seq.countBy id
    |> Seq.sortBy (snd >> (~-))
    |> Seq.truncate 40
    |> Seq.filter (fun (a, _) -> searchableToken.IsMatch a)
    |> dict


let summaryJson (note: Diary) =
    note.Note |> TipTap.getTextFromNote |> freqs |> Json.Convert.toJson

let updateNoteSummary conn (note_id: string) (user_id: int) =
    let summary =
        Diary.DiaryByUserIDAndID conn { NoteId = note_id; UserId = user_id }
        |> summaryJson

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
        let summary = summaryJson note
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
            let summary = summaryJson note
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
