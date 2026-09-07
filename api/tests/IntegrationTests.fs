module IntegrationTests

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Falco
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Npgsql
open Testcontainers.PostgreSql
open Xunit

module private TestDatabase =
    let databaseUrl =
        match Environment.GetEnvironmentVariable("LOGBOOK_TEST_DATABASE_URL") with
        | value when String.IsNullOrWhiteSpace(value) -> None
        | value -> Some value

    let private appendTimeouts (connectionString: string) =
        connectionString.TrimEnd(';') + ";Timeout=300;CommandTimeout=300"

    let connectionStringFromUrl databaseUrl =
        Npgsql.FSharp.Sql.fromUri (Uri(databaseUrl)) |> appendTimeouts

type DatabaseFactAttribute() =
    inherit FactAttribute()

type IntegrationTestFixture() =
    let jwtSecret = "integration-test-secret-that-is-long-enough-for-hs256"
    let jwtAudience = "logbook-integration-tests"

    let mutable postgres: PostgreSqlContainer option = None
    let mutable dataSource: NpgsqlDataSource option = None
    let mutable server: TestServer option = None

    let buildPostgresContainer () =
        PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("logbook_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build()

    let containerConnectionString () =
        task {
            match TestDatabase.databaseUrl with
            | Some databaseUrl -> return TestDatabase.connectionStringFromUrl databaseUrl
            | None ->
                let container = buildPostgresContainer ()
                postgres <- Some container
                do! container.StartAsync()
                return container.GetConnectionString().TrimEnd(';') + ";Timeout=300;CommandTimeout=300"
        }

    let initializeDatabase (dataSource: NpgsqlDataSource) =
        Database.InitDB.init dataSource |> ignore

        use conn = dataSource.OpenConnection()
        use cmd =
            new NpgsqlCommand(
                "TRUNCATE TABLE summary, diary, auth_user, jwt_secrets RESTART IDENTITY CASCADE;",
                conn
            )

        cmd.ExecuteNonQuery() |> ignore

    let createServer dataSource jwtConfig =
        let builder =
            WebHostBuilder()
                .ConfigureServices(fun services ->
                    services.AddRouting() |> ignore
                    services |> AppStartup.addDatabase dataSource |> ignore
                    services |> AppStartup.addAuthentication jwtConfig |> ignore
                    services |> AppStartup.addSummaryBackgroundProcessing |> ignore
                    services |> AppStartup.addIndexBackgroundProcessing |> ignore
                    services |> AppStartup.addApplicationServices |> ignore
                    services |> AppStartup.addBackgroundJobsWorker |> ignore
                    services |> AppStartup.addCors |> ignore)
                .Configure(fun app ->
                    app.UseRouting()
                        .UseCors(AppStartup.corsPolicyName)
                        .Use(GlobalErrorHandler.useMiddleware)
                        .UseAuthentication()
                        .Use(AppStartup.requireAuthenticatedApiRoutes)
                        .UseFalco(ApiRoutes.endpoints)
                    |> ignore)

        new TestServer(builder)

    member _.CreateClient() =
        match server with
        | Some server -> server.CreateClient()
        | None -> invalidOp "Integration test server has not been initialized."

    member _.WithConnection(action: NpgsqlConnection -> 'T) =
        match dataSource with
        | Some dataSource ->
            use conn = dataSource.OpenConnection()
            action conn
        | None -> invalidOp "Integration test database has not been initialized."

    interface IAsyncLifetime with
        member _.InitializeAsync() =
            task {
                Environment.SetEnvironmentVariable("JWT_SECRET", jwtSecret)
                Environment.SetEnvironmentVariable("JWT_AUDIENCE", jwtAudience)

                let! connectionString = containerConnectionString ()
                let initializedDataSource = Database.Connection.createDataSource connectionString
                dataSource <- Some initializedDataSource

                initializeDatabase initializedDataSource
                let jwtConfig = AppStartup.initializeJwtConfig initializedDataSource
                server <- Some(createServer initializedDataSource jwtConfig)
            }

        member _.DisposeAsync() =
            task {
                match server with
                | Some server -> server.Dispose()
                | None -> ()

                match dataSource with
                | Some dataSource -> dataSource.Dispose()
                | None -> ()

                match postgres with
                | Some postgres -> do! postgres.DisposeAsync().AsTask()
                | None -> ()
            }

type IntegrationTests(fixture: IntegrationTestFixture) =
    let jsonContent value =
        new StringContent(Json.Convert.toJson value, Encoding.UTF8, "application/json")

    let uniqueEmail prefix =
        sprintf "%s-%s@example.test" prefix (Guid.NewGuid().ToString("N"))

    let uniqueNoteId () =
        let offsetDays = Random.Shared.Next(0, 3650)
        DateTime(2020, 1, 1).AddDays(offsetDays).ToString("yyyyMMdd")

    let readJson (response: HttpResponseMessage) =
        task {
            let! body = response.Content.ReadAsStringAsync()
            return JsonDocument.Parse(body)
        }

    let login (client: HttpClient) username password =
        task {
            let credentials =
                {| username = username
                   password = password |}

            let! response = client.PostAsync(ApiPaths.login, jsonContent credentials)

            if response.StatusCode <> HttpStatusCode.OK then
                return response, None
            else
                use! json = readJson response
                let token = json.RootElement.GetProperty("accessToken").GetString()
                return response, Some token
        }

    let register (client: HttpClient) username password =
        task {
            let credentials =
                {| username = username
                   password = password |}

            let! response = client.PostAsync(ApiPaths.register, jsonContent credentials)

            if response.StatusCode <> HttpStatusCode.Created then
                return response, None
            else
                use! json = readJson response
                let token = json.RootElement.GetProperty("accessToken").GetString()
                return response, Some token
        }

    /// Register a new user; if already registered, log in instead.
    let ensureUserToken (client: HttpClient) username password =
        task {
            let! regResponse, regToken = register client username password

            match regToken with
            | Some token -> return token
            | None ->
                let! _, loginToken = login client username password
                return loginToken.Value
        }

    let sendWithToken (client: HttpClient) (method: HttpMethod) (path: string) token content =
        task {
            use request = new HttpRequestMessage(method, path)
            request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", token)

            match content with
            | Some requestContent -> request.Content <- requestContent
            | None -> ()

            return! client.SendAsync(request)
        }

    let tipTapDoc text =
        {| ``type`` = "doc"
           content =
            [| {| ``type`` = "paragraph"
                  content = [| {| ``type`` = "text"; text = text |} |] |} |] |}
        |> Json.Convert.toJson

    let tipTapTodoDoc text =
        {| ``type`` = "doc"
           content =
            [| {| ``type`` = "taskList"
                  content =
                    [| {| ``type`` = "taskItem"
                          attrs = {| ``checked`` = false |}
                          content =
                            [| {| ``type`` = "paragraph"
                                  content = [| {| ``type`` = "text"; text = text |} |] |} |] |} |] |} |] |}
        |> Json.Convert.toJson

    let rec waitForSummary (client: HttpClient) token noteId attempts =
        task {
            let! listResponse =
                sendWithToken client HttpMethod.Get ApiPaths.diary token None

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode)

            use! summaries = readJson listResponse

            let found =
                summaries.RootElement.EnumerateArray()
                |> Seq.exists (fun summary -> summary.GetProperty("noteId").GetString() = noteId)

            if found || attempts <= 1 then
                return found
            else
                do! Task.Delay(100)
                return! waitForSummary client token noteId (attempts - 1)
        }

    let rec waitForSearchResult (client: HttpClient) token (searchTerm: string) (noteId: string) attempts =
        task {
            let! searchResponse =
                sendWithToken client HttpMethod.Get $"/api/diary/search?q={searchTerm}" token None

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode)

            use! results = readJson searchResponse

            let found =
                results.RootElement.EnumerateArray()
                |> Seq.exists (fun r -> r.GetProperty("noteId").GetString() = noteId)

            if found || attempts <= 1 then
                return found
            else
                do! Task.Delay(100)
                return! waitForSearchResult client token searchTerm noteId (attempts - 1)
        }

    let rec waitForTodoDocContains (client: HttpClient) token (noteId: string) attempts =
        task {
            let! todoResponse =
                sendWithToken client HttpMethod.Get ApiPaths.todo token None

            Assert.Equal(HttpStatusCode.OK, todoResponse.StatusCode)

            let! body = todoResponse.Content.ReadAsStringAsync()
            let found = body.Contains(noteId, StringComparison.Ordinal)

            if found || attempts <= 1 then
                return found, body
            else
                do! Task.Delay(100)
                return! waitForTodoDocContains client token noteId (attempts - 1)
        }

    interface IClassFixture<IntegrationTestFixture>

    [<DatabaseFact>]
    member _.``register creates a new user, login requires existing user, and reject wrong passwords``() =
        task {
            use client = fixture.CreateClient()
            let email = uniqueEmail "auth"

            // Login before registering → unauthorized (user does not exist)
            let! preLogin, preToken = login client email "any-password"
            Assert.Equal(HttpStatusCode.Unauthorized, preLogin.StatusCode)
            Assert.True(preToken.IsNone)

            // Register a new user → 201 with token
            let! regResponse, regToken = register client email "correct-password"
            Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode)
            Assert.True(regToken.IsSome)

            // Register again with same email → 409 conflict
            let! dupReg, dupToken = register client email "another-password"
            Assert.Equal(409, int dupReg.StatusCode)
            Assert.True(dupToken.IsNone)

            // Login with correct password → 200 with token
            let! goodLogin, goodToken = login client email "correct-password"
            Assert.Equal(HttpStatusCode.OK, goodLogin.StatusCode)
            Assert.True(goodToken.IsSome)

            // Login with wrong password → 401 unauthorized
            let! badLogin, badToken = login client email "wrong-password"
            Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode)
            Assert.True(badToken.IsNone)
        }

    [<DatabaseFact>]
    member _.``diary save persists notes for the authenticated user``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "diary"
            let noteId = uniqueNoteId ()
            let noteText = "Integration diary save sentinel"
            let! token = ensureUserToken client username "password"

            let diary =
                {| id = 0
                   userId = 0
                   noteId = noteId
                   note = tipTapDoc noteText
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put $"/api/diary/{noteId}" token (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode)

            let! getResponse =
                sendWithToken client HttpMethod.Get $"/api/diary/{noteId}" token None

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode)

            use! savedDiary = readJson getResponse
            Assert.Equal(noteId, savedDiary.RootElement.GetProperty("noteId").GetString())
            Assert.Contains(noteText, savedDiary.RootElement.GetProperty("note").GetString())

            let! hasSummary = waitForSummary client token noteId 60
            Assert.True(hasSummary)
        }

    [<DatabaseFact>]
    member _.``search returns saved diary notes by indexed terms``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "search"
            let noteId = uniqueNoteId ()
            let searchTerm = "integrationneedle"
            let! token = ensureUserToken client username "password"

            let diary =
                {| id = 0
                   userId = 0
                   noteId = noteId
                   note = tipTapDoc $"Searchable {searchTerm} note"
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put $"/api/diary/{noteId}" token (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode)

            let! found = waitForSearchResult client token searchTerm noteId 80
            Assert.True(found)
        }

    [<DatabaseFact>]
    member _.``todo endpoint returns precomputed todos from saved notes``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "todo"
            let noteId = uniqueNoteId ()
            let todoText = "Persisted todo sentinel"
            let! token = ensureUserToken client username "password"

            let diary =
                {| id = 0
                   userId = 0
                   noteId = noteId
                   note = tipTapTodoDoc todoText
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put $"/api/diary/{noteId}" token (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode)

            let! found, todoBody = waitForTodoDocContains client token noteId 80
            Assert.True(found)
            Assert.Contains(todoText, todoBody)
        }

    [<DatabaseFact>]
    member _.``protected api routes reject missing or invalid bearer tokens``() =
        task {
            use client = fixture.CreateClient()

            let! missingTokenResponse = client.GetAsync(ApiPaths.diaryIds)
            Assert.Equal(HttpStatusCode.Unauthorized, missingTokenResponse.StatusCode)

            let! invalidTokenResponse =
                sendWithToken client HttpMethod.Get ApiPaths.diaryIds "not-a-valid-jwt" None

            Assert.Equal(HttpStatusCode.Unauthorized, invalidTokenResponse.StatusCode)
        }

    [<DatabaseFact>]
    member _.``diary routes reject invalid note ids``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "invalid-note-id"
            let! token = ensureUserToken client username "password"

            let! getResponse =
                sendWithToken client HttpMethod.Get "/api/diary/undefined" token None

            Assert.Equal(HttpStatusCode.BadRequest, getResponse.StatusCode)

            let diary =
                {| id = 0
                   userId = 0
                   noteId = "20240101"
                   note = tipTapDoc "Invalid route id should not save"
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put "/api/diary/not-a-date" token (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.BadRequest, saveResponse.StatusCode)
        }

    [<DatabaseFact>]
    member _.``admin can deactivate users while preserving data and deactivated tokens stop authenticating``() =
        task {
            use client = fixture.CreateClient()
            let adminEmail = uniqueEmail "admin-delete"
            let targetEmail = uniqueEmail "delete-target"

            let! _ = ensureUserToken client adminEmail "password"

            fixture.WithConnection(fun conn ->
                use cmd = new NpgsqlCommand("UPDATE auth_user SET is_superuser = true WHERE email = @email;", conn)
                cmd.Parameters.AddWithValue("email", adminEmail) |> ignore
                cmd.ExecuteNonQuery() |> ignore)

            let! adminToken = ensureUserToken client adminEmail "password"

            let! targetToken = ensureUserToken client targetEmail "password"

            let targetUserId =
                fixture.WithConnection(fun conn ->
                    use cmd = new NpgsqlCommand("SELECT id FROM auth_user WHERE email = @email;", conn)
                    cmd.Parameters.AddWithValue("email", targetEmail) |> ignore
                    cmd.ExecuteScalar() :?> int)

            let diary =
                {| id = 0
                   userId = 0
                   noteId = "20240102"
                   note = tipTapDoc "Will be preserved"
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put "/api/diary/20240102" targetToken (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode)

            let! deleteResponse =
                sendWithToken client HttpMethod.Delete $"/api/users/{targetUserId}" adminToken None

            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode)

            let userStillExists =
                fixture.WithConnection(fun conn ->
                    use cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM auth_user WHERE id = @user_id);", conn)
                    cmd.Parameters.AddWithValue("user_id", targetUserId) |> ignore
                    cmd.ExecuteScalar() :?> bool)

            let diaryStillExists =
                fixture.WithConnection(fun conn ->
                    use cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM diary WHERE user_id = @user_id);", conn)
                    cmd.Parameters.AddWithValue("user_id", targetUserId) |> ignore
                    cmd.ExecuteScalar() :?> bool)

            let userIsActive =
                fixture.WithConnection(fun conn ->
                    use cmd = new NpgsqlCommand("SELECT is_active FROM auth_user WHERE id = @user_id;", conn)
                    cmd.Parameters.AddWithValue("user_id", targetUserId) |> ignore
                    cmd.ExecuteScalar() :?> bool)

            Assert.True(userStillExists)
            Assert.False(userIsActive)
            Assert.True(diaryStillExists)

            let! deletedUserResponse =
                sendWithToken client HttpMethod.Get ApiPaths.diaryIds targetToken None

            Assert.Equal(HttpStatusCode.Unauthorized, deletedUserResponse.StatusCode)
        }

    [<DatabaseFact>]
    member _.``sync detects conflicts and retries committed mutations without reverting later writes``() =
        task {
            use client = fixture.CreateClient()
            let! token = ensureUserToken client (uniqueEmail "sync") "password"
            let noteId = "20260907"
            let path = $"/api/sync/diary/{noteId}"
            let first = {| note = tipTapDoc "first"; baseRevision = "0"; mutationId = Guid.NewGuid().ToString() |}
            let! response = sendWithToken client HttpMethod.Put path token (Some(jsonContent first))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            use! firstJson = readJson response
            let revision = firstJson.RootElement.GetProperty("revision").GetString()
            let second = {| note = tipTapDoc "second"; baseRevision = revision; mutationId = Guid.NewGuid().ToString() |}
            let! secondResponse = sendWithToken client HttpMethod.Put path token (Some(jsonContent second))
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode)

            let! retry = sendWithToken client HttpMethod.Put path token (Some(jsonContent first))
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode)
            use! retryJson = readJson retry
            Assert.Equal(revision, retryJson.RootElement.GetProperty("revision").GetString())
            let! current = sendWithToken client HttpMethod.Get path token None
            use! currentJson = readJson current
            Assert.Contains("second", currentJson.RootElement.GetProperty("note").GetString())

            let conflict = {| note = tipTapDoc "other device"; baseRevision = revision; mutationId = Guid.NewGuid().ToString() |}
            let! rejected = sendWithToken client HttpMethod.Put path token (Some(jsonContent conflict))
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode)
            use! rejectedJson = readJson rejected
            Assert.Contains("second", rejectedJson.RootElement.GetProperty("current").GetProperty("note").GetString())

            let reused = {| first with note = tipTapDoc "different request" |}
            let! invalid = sendWithToken client HttpMethod.Put path token (Some(jsonContent reused))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode)
        }

    [<DatabaseFact>]
    member _.``sync reads do not create entries and changes include clears and isolate accounts``() =
        task {
            use client = fixture.CreateClient()
            let! token = ensureUserToken client (uniqueEmail "sync-feed") "password"
            let! otherToken = ensureUserToken client (uniqueEmail "sync-other") "password"
            let path = "/api/sync/diary/20260906"
            let! missing = sendWithToken client HttpMethod.Get path token None
            use! missingJson = readJson missing
            Assert.Equal("0", missingJson.RootElement.GetProperty("revision").GetString())
            let! legacyRead = sendWithToken client HttpMethod.Get "/api/diary/20260906" token None
            Assert.Equal(HttpStatusCode.OK, legacyRead.StatusCode)
            let! emptyFeed = sendWithToken client HttpMethod.Get "/api/sync/changes" token None
            use! emptyJson = readJson emptyFeed
            Assert.Equal(0, emptyJson.RootElement.GetProperty("entries").GetArrayLength())

            let first = {| note = tipTapDoc "clear me"; baseRevision = "0"; mutationId = Guid.NewGuid().ToString() |}
            let! saved = sendWithToken client HttpMethod.Put path token (Some(jsonContent first))
            use! savedJson = readJson saved
            let revision = savedJson.RootElement.GetProperty("revision").GetString()
            let clear = {| note = ""; baseRevision = revision; mutationId = Guid.NewGuid().ToString() |}
            let! cleared = sendWithToken client HttpMethod.Put path token (Some(jsonContent clear))
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode)
            let! changes = sendWithToken client HttpMethod.Get $"/api/sync/changes?cursor={revision}" token None
            use! changesJson = readJson changes
            Assert.Equal(1, changesJson.RootElement.GetProperty("entries").GetArrayLength())
            Assert.Equal("", (changesJson.RootElement.GetProperty("entries").[0].GetProperty("note").GetString()))
            let! other = sendWithToken client HttpMethod.Get "/api/sync/changes" otherToken None
            use! otherJson = readJson other
            Assert.Equal(0, otherJson.RootElement.GetProperty("entries").GetArrayLength())
        }

    [<DatabaseFact>]
    member _.``simultaneous creation of the same date has exactly one winner``() =
        task {
            use client = fixture.CreateClient()
            let! token = ensureUserToken client (uniqueEmail "sync-race") "password"
            let writes = [| "phone"; "laptop" |] |> Array.map (fun text ->
                let mutation = {| note = tipTapDoc text; baseRevision = "0"; mutationId = Guid.NewGuid().ToString() |}
                sendWithToken client HttpMethod.Put "/api/sync/diary/20260905" token (Some(jsonContent mutation)))
            let! responses = Task.WhenAll(writes)
            Assert.Equal(1, responses |> Array.filter (fun r -> r.StatusCode = HttpStatusCode.OK) |> Array.length)
            Assert.Equal(1, responses |> Array.filter (fun r -> r.StatusCode = HttpStatusCode.Conflict) |> Array.length)
        }

    [<DatabaseFact>]
    member _.``sync history pages resume without dropping entries``() =
        task {
            use client = fixture.CreateClient()
            let email = uniqueEmail "sync-pages"
            let! token = ensureUserToken client email "password"
            // Seed history without flooding the unrelated summary worker queue.
            fixture.WithConnection(fun conn ->
                use cmd = new NpgsqlCommand("INSERT INTO diary (user_id, note_id, note) SELECT u.id, to_char(date '2025-01-01' + day, 'YYYYMMDD'), '' FROM auth_user u CROSS JOIN generate_series(0, 100) day WHERE u.email = @email", conn)
                cmd.Parameters.AddWithValue("email", email) |> ignore
                cmd.ExecuteNonQuery() |> ignore)
            let! first = sendWithToken client HttpMethod.Get "/api/sync/changes" token None
            use! firstJson = readJson first
            Assert.Equal(100, firstJson.RootElement.GetProperty("entries").GetArrayLength())
            Assert.True(firstJson.RootElement.GetProperty("hasMore").GetBoolean())
            let cursor = firstJson.RootElement.GetProperty("cursor").GetString()
            let! second = sendWithToken client HttpMethod.Get $"/api/sync/changes?cursor={cursor}" token None
            use! secondJson = readJson second
            Assert.Equal(1, secondJson.RootElement.GetProperty("entries").GetArrayLength())
            Assert.False(secondJson.RootElement.GetProperty("hasMore").GetBoolean())
            let nextCursor = secondJson.RootElement.GetProperty("cursor").GetString()
            let! last = sendWithToken client HttpMethod.Get $"/api/sync/changes?cursor={nextCursor}" token None
            use! lastJson = readJson last
            Assert.Equal(0, lastJson.RootElement.GetProperty("entries").GetArrayLength())
        }
