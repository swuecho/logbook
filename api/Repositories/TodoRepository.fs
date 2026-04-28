module TodoRepository

open Npgsql

let getByUserId (conn: NpgsqlConnection) userId =
    Todo.GetTodoByUserId conn userId

let insertOrUpdate (conn: NpgsqlConnection) noteId userId todos =
    Todo.InsertOrUpdateTodo
        conn
        { NoteId = noteId
          UserId = userId
          Todos = todos }
    |> ignore

let delete (conn: NpgsqlConnection) noteId userId =
    Todo.DeleteTodo
        conn
        { NoteId = noteId
          UserId = userId }
    |> ignore
