module NoteQ

open Npgsql.FSharp
open System

let checkIdStale (id: string) (user_id: int) =
    Database.Config.connection ()
    |> Sql.query
        "select count(*) > 0 as stale from (select d.id  from diary as d  , summary  as s where d.user_id = @user_id and d.id = s.id and d.last_updated >  s.last_updated) as t where t.id = '@note_id'"
    |> Sql.parameters [ "note_id", Sql.string id; "user_id", Sql.int user_id ]
    |> Sql.executeRow (fun read -> read.bool "stale")

let insertSummary (id: string) (user_id: int) (summary: string) =
    Database.Config.connection ()
    |> Sql.query
        "insert INTO summary (id, user_id, content, last_updated) VALUES (@id, @user_id, @content, @last_updated) ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, last_updated =  EXCLUDED.last_updated"
    |> Sql.parameters
        [ "id", Sql.string id
          "user_id", Sql.int user_id
          "content", Sql.jsonb summary
          "last_updated", Sql.timestamptz (DateTime.UtcNow) ]
    |> Sql.executeNonQuery
