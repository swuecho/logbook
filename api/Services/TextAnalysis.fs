module TextAnalysis

open System
open System.Text.RegularExpressions
open JiebaNet

let private resourceDir = __SOURCE_DIRECTORY__ + "/../Resources/jieba"
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

let freqs text =
    let words = segment text

    words
    |> Seq.countBy id
    |> Seq.sortBy (snd >> (~-))
    |> Seq.truncate 40
    |> Seq.filter (fun (word, _) -> searchableToken.IsMatch word)
    |> dict

let searchIndexOfNote (note: string) =
    let searchText = note |> TipTap.getTextFromNote
    searchText, searchTerms searchText
