module SessionRepository

open System
open Npgsql

type ActiveSession = { UserId: int; IsAdmin: bool; ExpiresAt: DateTime }

let revoke (conn: NpgsqlConnection) tokenHash =
    use cmd = new NpgsqlCommand("UPDATE auth_session SET revoked_at = now() WHERE token_hash = @hash AND revoked_at IS NULL", conn)
    cmd.Parameters.AddWithValue("hash", tokenHash) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let create (conn: NpgsqlConnection) userId tokenHash expiresAt previousHash =
    if previousHash <> "" then revoke conn previousHash
    use cleanup = new NpgsqlCommand("DELETE FROM auth_session WHERE expires_at <= now()", conn)
    cleanup.ExecuteNonQuery() |> ignore
    use cmd = new NpgsqlCommand("INSERT INTO auth_session(token_hash, user_id, expires_at) VALUES (@hash, @user, @expires)", conn)
    cmd.Parameters.AddWithValue("hash", tokenHash) |> ignore
    cmd.Parameters.AddWithValue("user", userId) |> ignore
    cmd.Parameters.AddWithValue("expires", expiresAt) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let authenticate (conn: NpgsqlConnection) tokenHash userId =
    // Live account/role checks work across processes and restarts. Background API
    // requests count as activity; the absolute JWT/session deadline never slides.
    use cmd = new NpgsqlCommand("""
        UPDATE auth_session s SET last_seen_at = now()
        FROM auth_user u
        WHERE s.token_hash = @hash AND s.user_id = @user AND u.id = s.user_id
          AND u.is_active = true AND s.revoked_at IS NULL
          AND s.expires_at > now() AND s.last_seen_at > now() - interval '30 minutes'
        RETURNING s.user_id, u.is_superuser, s.expires_at
        """, conn)
    cmd.Parameters.AddWithValue("hash", tokenHash) |> ignore
    cmd.Parameters.AddWithValue("user", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some { UserId = reader.GetInt32(0); IsAdmin = reader.GetBoolean(1); ExpiresAt = reader.GetDateTime(2) }
    else None

let revokeAll (conn: NpgsqlConnection) userId =
    use cmd = new NpgsqlCommand("UPDATE auth_session SET revoked_at = now() WHERE user_id = @user AND revoked_at IS NULL", conn)
    cmd.Parameters.AddWithValue("user", userId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
