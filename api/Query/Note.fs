namespace Query

open Npgsql.FSharp
open System

open Models

module Note =

    let getNoteAll () =
        Database.Config.connection ()
        |> Sql.query "select id, note from diary order by id desc"
        |> Sql.execute Database.Record.Reader<Note>

    let getSummaryAll () =
        Database.Config.connection ()
        |> Sql.query "select id, content as note from summary order by id desc"
        |> Sql.execute Database.Record.Reader<Note>

    let checkIdStale (id: string) = 
        Database.Config.connection ()
        |> Sql.query "select count(*) > 0 as stale from (select d.id  from diary as d  , summary  as s where d.id = s.id and d.last_updated >  s.last_updated) as t where t.id = '@note_id'"
        |> Sql.parameters [ "note_id", Sql.string id ]
        |> Sql.executeRow (fun read -> read.bool "stale") 

    let getStaledIds () = 
        Database.Config.connection ()
        |> Sql.query "select d.id  as id from diary as d, summary  as s where d.id = s.id and d.last_updated >  s.last_updated UNION (select d.id as id from diary as d where d.id not in (select id from summary))"
        |> Sql.execute (fun read -> read.string "id") 

    let getSummaryLastUpdated (id: string) =
        Database.Config.connection ()
        |> Sql.query "select last_updated from summary where id = @id"
        |> Sql.parameters [ "id", Sql.string id ]
        |> Sql.execute (fun read -> read.dateTime "last_updated") 

    let insertSummary (id: string) (summary: string) =
        Database.Config.connection ()
        |> Sql.query "insert INTO summary (id, content, last_updated) VALUES (@id, @content, @last_updated) ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, last_updated =  EXCLUDED.last_updated"
        |> Sql.parameters [ "id", Sql.string id;
                            "content" , Sql.jsonb summary
                            "@last_updated", Sql.timestamptz (DateTime.UtcNow)
                           ]
        |> Sql.executeNonQuery

    /// get note by id
    let getNoteById id =
        Database.Config.connection ()
        |> Sql.query "select id, note from diary where id = @id"
        |> Sql.parameters [ "id", Sql.string id ]
        |> Sql.executeRowAsync Database.Record.Reader<Note>

    let getNoteByIdSync id =
        Database.Config.connection ()
        |> Sql.query "select id, note from diary where id = @id"
        |> Sql.parameters [ "id", Sql.string id ]
        |> Sql.executeRow Database.Record.Reader<Note>

    /// get note by id
    let getSummaryById id =
        Database.Config.connection ()
        |> Sql.query "select id, content from summary where id = @id"
        |> Sql.parameters [ "id", Sql.string id ]
        |> Sql.executeRow (fun read -> read.string "content") 

    /// upsert note
    let AddNote (note: Note) =
        Database.Config.connection ()
        |> Sql.query
            "INSERT INTO diary (id, user_id, note, last_updated) VALUES (@id, @user_id, @note, @last_updated)  ON CONFLICT (id) DO UPDATE SET note = EXCLUDED.note, last_updated =  EXCLUDED.last_updated"
        |> Sql.parameters [ "@id", Sql.string note.Id
                            "@user_id", Sql.int note.UserId
                            "@note", Sql.string note.Note 
                            "@last_updated", Sql.timestamptz (DateTime.UtcNow)
                            ]
        |> Sql.executeNonQueryAsync
