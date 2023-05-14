module NoteQ
    open Npgsql.FSharp
    open System

    let checkIdStale (id: string) (user_id: int) =
        Database.Config.connection ()
        |> Sql.query
            "select count(*) > 0 as stale from (select d.id  from diary as d  , summary  as s where d.user_id =@user_id and d.id = s.id and d.last_updated >  s.last_updated) as t where t.id = '@note_id'"
        |> Sql.parameters [ "note_id", Sql.string id; "user_id", Sql.int user_id ]
        |> Sql.executeRow (fun read -> read.bool "stale")

    let getStaledIds user_id =
        Database.Config.connection ()
        |> Sql.query
            "select d.id  as id from diary as d, summary as s where d.user_id = @user_id and d.id = s.id and d.last_updated >  s.last_updated UNION (select d.id as id from diary as d where d.id and d.user_id = @user_id not in (select id from summary where user_id = @user_id))"
        |> Sql.parameters [ "user_id", Sql.int user_id ]
        |> Sql.execute (fun read -> read.string "id")

    let getSummaryLastUpdated (id: string) user_id =
        Database.Config.connection ()
        |> Sql.query "select last_updated from summary where id = @id and user_id = @user_id"
        |> Sql.parameters [ "id", Sql.string id; "user_id", Sql.int user_id ]
        |> Sql.execute (fun read -> read.dateTime "last_updated")

    let insertSummary (id: string) (user_id: int) (summary: string) =
        Database.Config.connection ()
        |> Sql.query
            "insert INTO summary (id, user_id, content, last_updated) VALUES (@id, @user_id, @content, @last_updated) ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, last_updated =  EXCLUDED.last_updated"
        |> Sql.parameters
            [ "id", Sql.string id
              "user_id", Sql.int user_id
              "content", Sql.jsonb summary
              "@last_updated", Sql.timestamptz (DateTime.UtcNow) ]
        |> Sql.executeNonQuery


    /// upsert note
    let AddNote (note: Diary) =
        Database.Config.connection ()
        |> Sql.query
            "INSERT INTO diary (id, user_id, note, last_updated) VALUES (@id, @user_id, @note, @last_updated)  ON CONFLICT (id) DO UPDATE SET note = EXCLUDED.note, last_updated =  EXCLUDED.last_updated"
        |> Sql.parameters
            [ "@id", Sql.string note.Id
              "@user_id", Sql.int note.UserId
              "@note", Sql.string note.Note
              "@last_updated", Sql.timestamptz (DateTime.UtcNow) ]
        |> Sql.executeNonQueryAsync
