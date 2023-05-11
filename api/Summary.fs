namespace Summary

open System.Text
open JiebaNet
open Models
open FSharp.Data



module Jieba =
    let freqs text =
        let wordRegex = RegularExpressions.Regex "\\w"
        let resourceDir =
            __SOURCE_DIRECTORY__ + "/Resources/jieba"

        Segmenter.ConfigManager.ConfigFileBaseDir <- resourceDir

        let seg = Segmenter.JiebaSegmenter()
        let words = seg.Cut(text)

        words
        |> Seq.countBy id
        |> Seq.sortBy (snd >> (~-))
        |> Seq.truncate 40
        |> Seq.filter (fun (a, b) -> wordRegex.IsMatch a )
        |> dict

    /// Join a sequence of strings using a delimiter.
    /// Equivalent to String.Join() but without arrays.
    let join  (delim : string) (items : seq<string>) =
    // Collect the result in the string builder buffer
    // The end-sequence will be "item1,delim,...itemN,delim"
        let buff = 
            Seq.fold 
                (fun (buff :StringBuilder) (s:string) -> buff.Append(s).Append(delim)) 
                (StringBuilder()) 
                items
        buff.Remove(buff.Length-delim.Length, delim.Length).ToString()

    let rec getContent (jv: JsonValue)= 
        let extractProperty (x,y) = 
           match (x,y) with 
            | ("content", y)  -> getContent y
            | ("text", y) -> string y 
            | (_, y) ->  " "
        match jv with 
            // JsonValue.Array(elements) -> Array.map getContent elements
            | JsonValue.Array(arr) -> arr |> Array.map getContent |> join " "
            | JsonValue.Record(record) -> record |> Array.map extractProperty  |> join " "
            | JsonValue.String(str) -> str
            | JsonValue.Number(n) ->  n |> string
            | JsonValue.Float(f) ->  f |> string
            | JsonValue.Boolean(b) ->  b |> string
            | JsonValue.Null ->  ""

    let getTextFromNote (note: Note) = 
        let content = sprintf "%s" note.Note
        match JsonValue.TryParse content with 
            | Some (json) ->  getContent json
            | None -> content 

    let insertSummary (note: Note) =
        let summaryJson =  note |> getTextFromNote |> freqs |> Json.Convert.toJson
        // store as jsonb
        summaryJson
    
    let updateNoteSummary (id: string) = 
        let summary = Query.Note.getNoteByIdSync id |> getTextFromNote |> freqs |> Json.Convert.toJson 
        Query.Note.insertSummary id summary |> ignore 


    let refreshSummary () = 
        let staledIds = Query.Note.getStaledIds ()
        printfn "%A" staledIds
        staledIds |> List.iter updateNoteSummary 

    let noteSummary (note: Note) =
        // get summary directly from note
        // check summary is exits
        let nid = note.Id
        printfn "%s" nid
        let noteDt = Query.Note.getSummaryLastUpdated nid
        match noteDt with
            | [] ->  
                    let summary =  note |> getTextFromNote |> freqs |> Json.Convert.toJson 
                    // insert new item
                    ignore(Query.Note.insertSummary nid summary);
                    summary
            | head:: _ ->   
                    let staled = Query.Note.checkIdStale nid 
                    if staled then
                        let summary =  note |> getTextFromNote |> freqs |> Json.Convert.toJson 
                            // update the summary and timestamp
                        ignore(Query.Note.insertSummary nid summary);
                        summary
                    else
                        let summary = Query.Note.getSummaryById nid
                        printfn "%s" summary;
                        summary
                            // fetch summary directly 

                    // check if stale
                    // checkIfStale nid head 

                        
        
        //printfn "%s" (myDate.ToString
        // select last_updated from summary
        // summary not exits  -> insert
        //      exits but stale ( summary.lastmodified < note.lastmodified ) ->  update
        //      exists and up to date -> return the summary content (this branch is majority)

        // note |> getTextFromNote |> freqs

    let freqsOfNote (note: Note) =
        { Id = note.Id
          Note = note |> noteSummary }
