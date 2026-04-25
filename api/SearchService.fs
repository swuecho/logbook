module SearchService

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
    |> Seq.filter (fun (word, _) -> searchableToken.IsMatch word)
    |> dict

let summaryJson (note: Diary) =
    note.Note |> TipTap.getTextFromNote |> freqs |> Json.Convert.toJson

let updateNoteSummary conn (noteId: string) (userId: int) =
    let summary =
        Diary.DiaryByUserIDAndID conn { NoteId = noteId; UserId = userId }
        |> summaryJson

    Summary.InsertSummary
        conn
        { NoteId = noteId
          UserId = userId
          Content = summary }
    |> ignore

let refreshSummary conn userId =
    let staledIds =
        Diary.GetStaleIdsOfUserId conn userId |> List.map (fun x -> x.NoteId)

    staledIds
    |> List.iter (fun staleDiaryId -> updateNoteSummary conn staleDiaryId userId)

let noteSummary conn (note: Diary) =
    let noteId = note.NoteId
    let updateParams: Summary.LastUpdatedParams = { NoteId = noteId; UserId = note.UserId }

    let noteDt = [ Summary.LastUpdated conn updateParams ]

    match noteDt with
    | [] ->
        let summary = summaryJson note

        Summary.InsertSummary
            conn
            { NoteId = noteId
              Content = summary
              UserId = note.UserId }
        |> ignore

        summary
    | _ :: _ ->
        let staled = Diary.CheckIdStale conn { NoteId = noteId; UserId = note.UserId }

        if staled then
            let summary = summaryJson note

            Summary.InsertSummary
                conn
                { NoteId = noteId
                  Content = summary
                  UserId = note.UserId }
            |> ignore

            summary
        else
            let summary =
                Summary.GetSummaryByUserIDAndID conn { NoteId = noteId; UserId = note.UserId }

            summary.Content

let freqsOfNote conn (note: Diary) =
    { Id = note.Id
      NoteId = note.NoteId
      Note = note |> noteSummary conn
      UserId = note.UserId
      LastUpdated = note.LastUpdated }
