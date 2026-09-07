module DiarySyncService

open System
open Npgsql
open Database
open ApplicationContracts

type Entry = { NoteId: string; Note: string; Revision: string }
type Mutation = { Note: string; BaseRevision: string; MutationId: string }
type SaveResult = Saved of Entry | Conflict of Entry | InvalidMutation

let private readEntry (reader: System.Data.Common.DbDataReader) =
    { NoteId = reader.GetString(0); Note = reader.GetString(1); Revision = reader.GetInt64(2).ToString() }

let private getEntry (conn: NpgsqlConnection) userId noteId =
    use cmd = new NpgsqlCommand("SELECT note_id, note, revision FROM diary_sync_entry WHERE user_id = @user AND note_id = @note", conn)
    cmd.Parameters.AddWithValue("user", userId) |> ignore
    cmd.Parameters.AddWithValue("note", noteId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then readEntry reader
    else { NoteId = noteId; Note = ""; Revision = "0" }

let get (db: DbSession) userId noteId =
    db.WithConnection(fun conn -> getEntry conn userId noteId)

let changes (db: DbSession) userId cursor =
    db.WithConnection(fun conn ->
        use cmd = new NpgsqlCommand("SELECT note_id, note, revision FROM diary_sync_entry WHERE user_id = @user AND revision > @cursor ORDER BY revision LIMIT 101", conn)
        cmd.Parameters.AddWithValue("user", userId) |> ignore
        cmd.Parameters.AddWithValue("cursor", cursor) |> ignore
        use reader = cmd.ExecuteReader()
        let rows = ResizeArray<Entry>()
        while reader.Read() do rows.Add(readEntry reader)
        let entries = rows |> Seq.truncate 100 |> Seq.toArray
        let nextCursor = if entries.Length = 0 then cursor.ToString() else entries[entries.Length - 1].Revision
        {| Entries = entries; Cursor = nextCursor; HasMore = rows.Count > 100 |})

let save (db: DbSession) (publisher: IBackgroundJobPublisher) userId noteId (mutation: Mutation) =
    match Int64.TryParse mutation.BaseRevision, Guid.TryParse mutation.MutationId with
    | (true, baseRevision), (true, mutationId) when baseRevision >= 0L && not (isNull mutation.Note) ->
        let requestHash = Convert.ToHexString(Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes mutation.Note))
        let result, changed = db.WithTransaction(fun conn ->
            // Serialize all sync writes for this account before checking revisions.
            use ensure = new NpgsqlCommand("INSERT INTO diary_sync_state (user_id) VALUES (@user) ON CONFLICT DO NOTHING", conn)
            ensure.Parameters.AddWithValue("user", userId) |> ignore
            ensure.ExecuteNonQuery() |> ignore
            use gate = new NpgsqlCommand("SELECT revision FROM diary_sync_state WHERE user_id = @user FOR UPDATE", conn)
            gate.Parameters.AddWithValue("user", userId) |> ignore
            gate.ExecuteScalar() |> ignore

            let receipt =
                use cmd = new NpgsqlCommand("SELECT note_id, is_empty, revision, requested_hash, base_revision FROM diary_sync_receipt WHERE user_id = @user AND mutation_id = @mutation", conn)
                cmd.Parameters.AddWithValue("user", userId) |> ignore
                cmd.Parameters.AddWithValue("mutation", mutationId) |> ignore
                use reader = cmd.ExecuteReader()
                if reader.Read() then
                    let entry = { NoteId = reader.GetString(0); Note = (if reader.GetBoolean(1) then "" else mutation.Note); Revision = reader.GetInt64(2).ToString() }
                    Some(entry, reader.GetString(3), reader.GetInt64(4))
                else None

            match receipt with
            | Some(entry, requested, originalBase) ->
                if entry.NoteId = noteId && requested = requestHash && originalBase = baseRevision then Saved entry, false
                else InvalidMutation, false
            | None ->
                let current = getEntry conn userId noteId
                if current.Revision <> mutation.BaseRevision then Conflict current, false
                else
                    let note = if TipTap.isEffectivelyEmpty mutation.Note then "" else mutation.Note
                    if current.Revision = "0" || current.Note <> note then
                        DiaryRepository.addOrUpdate conn noteId userId note |> ignore
                    let saved = getEntry conn userId noteId
                    use cmd = new NpgsqlCommand("INSERT INTO diary_sync_receipt (user_id, mutation_id, note_id, requested_hash, base_revision, is_empty, revision) VALUES (@user, @mutation, @id, @requested, @base, @empty, @revision)", conn)
                    cmd.Parameters.AddWithValue("user", userId) |> ignore
                    cmd.Parameters.AddWithValue("mutation", mutationId) |> ignore
                    cmd.Parameters.AddWithValue("id", noteId) |> ignore
                    cmd.Parameters.AddWithValue("requested", requestHash) |> ignore
                    cmd.Parameters.AddWithValue("base", baseRevision) |> ignore
                    cmd.Parameters.AddWithValue("empty", saved.Note = "") |> ignore
                    cmd.Parameters.AddWithValue("revision", Int64.Parse saved.Revision) |> ignore
                    cmd.ExecuteNonQuery() |> ignore
                    Saved saved, current.Revision <> saved.Revision)
        if changed then
            let noteRef = { UserId = userId; NoteId = noteId }
            publisher.EnqueueSummaryUpdate noteRef
            publisher.EnqueueIndexUpdate noteRef
        result
    | _ -> InvalidMutation
