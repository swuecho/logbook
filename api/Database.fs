namespace Database

open System
open Npgsql.FSharp
open FSharp.Reflection

module Config = 
    /// Custom operator for combining paths
    let postgresDSN =
        try
            System.Environment.GetEnvironmentVariable("BESTQA_PG_DSN")
        with _ -> raise (System.Exception "check if BESTQA_FS_PORT is set")
    let connection () = postgresDSN |> Sql.connect


module Record =
// generate a function of type RowReader -> 't that looks for fields to map based on lowercase field names
 let  Reader<'t> =
    let createRecord = FSharpValue.PreComputeRecordConstructor typeof<'t>
    let make values = createRecord values :?> 't
    let fields = FSharpType.GetRecordFields typeof<'t> |> Array.map (fun p -> p.Name, p.PropertyType)

    let readField (r: RowReader) (n: string) (t: System.Type) =
        if   t = typeof<int> then r.int n |> box
        elif t = typeof<int option> then r.intOrNone n |> box
        elif t = typeof<int16> then r.int16 n |> box
        elif t = typeof<int16 option> then r.int16OrNone n |> box
        elif t = typeof<int []> then r.intArray n |> box
        elif t = typeof<int [] option> then r.intArrayOrNone n |> box
        elif t = typeof<string []> then r.stringArray n |> box
        elif t = typeof<string [] option> then r.stringArrayOrNone n |> box
        elif t = typeof<int64> then r.int64 n |> box
        elif t = typeof<int64 option> then r.int64OrNone n |> box
        elif t = typeof<string> then r.string n |> box
        elif t = typeof<string option> then r.stringOrNone n |> box
        elif t = typeof<bool> then r.bool n |> box
        elif t = typeof<bool option> then r.boolOrNone n |> box
        elif t = typeof<decimal> then r.decimal n |> box
        elif t = typeof<decimal option> then r.decimalOrNone n |> box
        elif t = typeof<double> then r.double n |> box
        elif t = typeof<double option> then r.doubleOrNone n |> box
        elif t = typeof<DateTime> then r.dateTime n |> box
        elif t = typeof<DateTime option> then r.dateTimeOrNone n |> box
        elif t = typeof<Guid> then r.uuid n |> box
        elif t = typeof<Guid option> then r.uuidOrNone n |> box
        elif t = typeof<byte[]> then r.bytea n |> box
        elif t = typeof<byte[] option> then r.byteaOrNone n |> box
        elif t = typeof<float> then r.float n |> box
        elif t = typeof<float option> then r.floatOrNone n |> box
        elif t = typeof<Guid []> then r.uuidArray n |> box
        elif t = typeof<Guid [] option> then r.uuidArrayOrNone n |> box
        else
            failwithf "Could not read column '%s' as %s" n t.FullName

    fun (reader: RowReader) ->
        let values = [| for (name, ty) in fields do readField reader (name.ToLowerInvariant()) ty |]
        make values
