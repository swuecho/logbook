module Util
open System.Text

let getEnvVar varName = System.Environment.GetEnvironmentVariable(varName) 

/// Join a sequence of strings using a delimiter.
/// Equivalent to String.Join() but without arrays.
let join (delim: string) (items: seq<string>) =
    // Collect the result in the string builder buffer
    // The end-sequence will be "item1,delim,...itemN,delim"
    let buff =
        Seq.fold (fun (buff: StringBuilder) (s: string) -> buff.Append(s).Append(delim)) (StringBuilder()) items

    buff.Remove(buff.Length - delim.Length, delim.Length).ToString()
