module VaultService

open System
open System.Text.Json
open Npgsql
open Database

type Snapshot = { Envelope: string; Revision: string }
type Mutation = { Envelope: string; BaseRevision: string }
type SaveResult = Saved of Snapshot | Conflict | Invalid

let private exactFields (element: JsonElement) names =
    element.ValueKind = JsonValueKind.Object
    && (element.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Seq.sort |> Seq.toList) = List.sort names

let private base64 (element: JsonElement) minLength maxLength =
    let value = element.GetString()
    if isNull value || value.Length > maxLength * 2 + 16 then false
    else
        let bytes = Convert.FromBase64String(value)
        bytes.Length >= minLength && bytes.Length <= maxLength && Convert.ToBase64String(bytes) = value

let private boxValid (element: JsonElement) minLength maxLength =
    exactFields element [ "iv"; "data" ]
    && base64 (element.GetProperty("iv")) 12 12
    && base64 (element.GetProperty("data")) minLength maxLength

// Restrict the format and resource cost, not just JSON validity. The server never decrypts.
let validEnvelope (value: string) =
    try
        if isNull value || Text.Encoding.UTF8.GetByteCount(value) > 1500000 then false
        else
            use doc = JsonDocument.Parse(value)
            let e = doc.RootElement
            exactFields e [ "format"; "version"; "id"; "kdf"; "master"; "recovery"; "payload" ]
            && e.GetProperty("format").GetString() = "logbook-vault"
            && e.GetProperty("version").GetInt32() = 1
            && (match Guid.TryParse(e.GetProperty("id").GetString()) with | true, id -> id.ToString() = e.GetProperty("id").GetString() | _ -> false)
            && (let kdf = e.GetProperty("kdf")
                exactFields kdf [ "name"; "iterations"; "salt" ]
                && kdf.GetProperty("name").GetString() = "PBKDF2-SHA256"
                && kdf.GetProperty("iterations").GetInt32() = 600000
                && base64 (kdf.GetProperty("salt")) 16 16)
            && boxValid (e.GetProperty("master")) 48 48
            && boxValid (e.GetProperty("recovery")) 48 48
            && boxValid (e.GetProperty("payload")) 16 1000016
    with _ -> false

let get (db: DbSession) userId =
    db.WithConnection(fun conn ->
        use cmd = new NpgsqlCommand("SELECT envelope, revision FROM password_vault WHERE user_id = @user", conn)
        cmd.Parameters.AddWithValue("user", userId) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some { Envelope = reader.GetString(0); Revision = reader.GetInt64(1).ToString() }
        else None)

let save (db: DbSession) userId (mutation: Mutation) =
    match Int64.TryParse mutation.BaseRevision with
    | true, revision when revision >= 0L && revision < Int64.MaxValue && validEnvelope mutation.Envelope ->
        db.WithConnection(fun conn ->
            // A single conditional statement provides atomic compare-and-swap, including initialization.
            let sql =
                if revision = 0L then
                    "INSERT INTO password_vault(user_id, envelope) VALUES (@user, @envelope) ON CONFLICT (user_id) DO NOTHING RETURNING revision"
                else
                    "UPDATE password_vault SET envelope = @envelope, revision = revision + 1, updated_at = now() WHERE user_id = @user AND revision = @revision RETURNING revision"
            use cmd = new NpgsqlCommand(sql, conn)
            cmd.Parameters.AddWithValue("user", userId) |> ignore
            cmd.Parameters.AddWithValue("envelope", mutation.Envelope) |> ignore
            cmd.Parameters.AddWithValue("revision", revision) |> ignore
            match cmd.ExecuteScalar() with
            | :? int64 as next -> Saved { Envelope = mutation.Envelope; Revision = next.ToString() }
            | _ ->
                // A retry of the exact last write is safe after a lost response.
                use retry = new NpgsqlCommand("SELECT revision FROM password_vault WHERE user_id = @user AND revision = @next AND envelope = @envelope", conn)
                retry.Parameters.AddWithValue("user", userId) |> ignore
                retry.Parameters.AddWithValue("next", revision + 1L) |> ignore
                retry.Parameters.AddWithValue("envelope", mutation.Envelope) |> ignore
                match retry.ExecuteScalar() with
                | :? int64 as next -> Saved { Envelope = mutation.Envelope; Revision = next.ToString() }
                | _ -> Conflict)
    | _ -> Invalid
