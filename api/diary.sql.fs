// Code generated by sqlc. DO NOT EDIT.
// source: diary.sql


module Diary 

open Npgsql
open Npgsql.FSharp
open System





let createDiary = """-- name: CreateDiary :one
INSERT INTO diary (id, note) VALUES (@id, @note)
RETURNING id, user_id, note, last_updated
"""


type CreateDiaryParams = {
  Id: string;
  Note: string;
}

let CreateDiary (db: NpgsqlConnection)  (arg: CreateDiaryParams)  =
  
  let reader = fun (read:RowReader) -> {
    Id = read.string "id"
    UserId = read.int "user_id"
    Note = read.string "note"
    LastUpdated = read.dateTime "last_updated"}
  

  db
  |> Sql.existingConnection
  |> Sql.query createDiary
  |> Sql.parameters  [ "@id", Sql.string arg.Id; "@note", Sql.string arg.Note ]
  |> Sql.executeRow reader















let deleteDiary = """-- name: DeleteDiary :exec
DELETE FROM diary WHERE id = @id
"""






let DeleteDiary (db: NpgsqlConnection)  (id: string)  = 
  db 
  |> Sql.existingConnection
  |> Sql.query deleteDiary
  |> Sql.parameters  [ "@id", Sql.string id ]
  |> Sql.executeNonQuery








let diaryByID = """-- name: DiaryByID :one
SELECT id, user_id, note, last_updated FROM diary WHERE id = @id
"""



let DiaryByID (db: NpgsqlConnection)  (id: string)  =
  
  let reader = fun (read:RowReader) -> {
    Id = read.string "id"
    UserId = read.int "user_id"
    Note = read.string "note"
    LastUpdated = read.dateTime "last_updated"}
  

  db
  |> Sql.existingConnection
  |> Sql.query diaryByID
  |> Sql.parameters  [ "@id", Sql.string id ]
  |> Sql.executeRow reader



















let listDiaries = """-- name: ListDiaries :many
SELECT id, user_id, note, last_updated FROM diary ORDER BY last_updated DESC
"""




let ListDiaries (db: NpgsqlConnection)  =
  let reader = fun (read:RowReader) -> {
    Id = read.string "id"
    UserId = read.int "user_id"
    Note = read.string "note"
    LastUpdated = read.dateTime "last_updated"}
  db 
  |> Sql.existingConnection
  |> Sql.query listDiaries
  |> Sql.execute reader












let updateDiary = """-- name: UpdateDiary :one
UPDATE diary SET note = @note, last_updated = NOW() WHERE id = @id
RETURNING id, user_id, note, last_updated
"""


type UpdateDiaryParams = {
  Id: string;
  Note: string;
}

let UpdateDiary (db: NpgsqlConnection)  (arg: UpdateDiaryParams)  =
  
  let reader = fun (read:RowReader) -> {
    Id = read.string "id"
    UserId = read.int "user_id"
    Note = read.string "note"
    LastUpdated = read.dateTime "last_updated"}
  

  db
  |> Sql.existingConnection
  |> Sql.query updateDiary
  |> Sql.parameters  [ "@id", Sql.string arg.Id; "@note", Sql.string arg.Note ]
  |> Sql.executeRow reader













