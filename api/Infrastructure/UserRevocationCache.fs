module UserRevocationCache

open System.Collections.Concurrent
open Npgsql

let private deactivatedUsers = ConcurrentDictionary<int, unit>()

let isDeactivated (userId: int) =
    deactivatedUsers.ContainsKey(userId)

let markDeactivated (userId: int) =
    deactivatedUsers.TryAdd(userId, ()) |> ignore

let initialize (dataSource: NpgsqlDataSource) =
    use conn = dataSource.OpenConnection()
    use cmd = new NpgsqlCommand("SELECT id FROM auth_user WHERE is_active = false", conn)
    use reader = cmd.ExecuteReader()

    while reader.Read() do
        deactivatedUsers.TryAdd(reader.GetInt32 0, ()) |> ignore
