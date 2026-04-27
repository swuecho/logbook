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
                    services |> AppStartup.addCors |> ignore)
                .Configure(fun app ->
                    app.UseRouting()
                        .UseCors(AppStartup.corsPolicyName)
                        .UseAuthentication()
                        .Use(AppStartup.requireAuthenticatedApiRoutes)
                        .UseFalco(ApiRoutes.endpoints)
                    |> ignore)

        new TestServer(builder)

    member _.CreateClient() =
        match server with
        | Some server -> server.CreateClient()
        | None -> invalidOp "Integration test server has not been initialized."

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
        "n" + Guid.NewGuid().ToString("N").Substring(0, 7)

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

    interface IClassFixture<IntegrationTestFixture>

    [<DatabaseFact>]
    member _.``login registers new users and rejects wrong passwords``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "auth"

            let! firstLogin, firstToken = login client username "correct-password"
            Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode)
            Assert.True(firstToken.IsSome)

            let! secondLogin, secondToken = login client username "correct-password"
            Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode)
            Assert.True(secondToken.IsSome)

            let! wrongPassword, wrongPasswordToken = login client username "wrong-password"
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode)
            Assert.True(wrongPasswordToken.IsNone)
        }

    [<DatabaseFact>]
    member _.``diary save persists notes for the authenticated user``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "diary"
            let noteId = uniqueNoteId ()
            let noteText = "Integration diary save sentinel"
            let! loginResponse, token = login client username "password"

            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode)
            let token = token.Value

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

            let! hasSummary = waitForSummary client token noteId 20
            Assert.True(hasSummary)
        }

    [<DatabaseFact>]
    member _.``search returns saved diary notes by indexed terms``() =
        task {
            use client = fixture.CreateClient()
            let username = uniqueEmail "search"
            let noteId = uniqueNoteId ()
            let searchTerm = "integrationneedle"
            let! loginResponse, token = login client username "password"

            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode)
            let token = token.Value

            let diary =
                {| id = 0
                   userId = 0
                   noteId = noteId
                   note = tipTapDoc $"Searchable {searchTerm} note"
                   lastUpdated = DateTime.UtcNow |}

            let! saveResponse =
                sendWithToken client HttpMethod.Put $"/api/diary/{noteId}" token (Some(jsonContent diary))

            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode)

            let! searchResponse =
                sendWithToken client HttpMethod.Get $"/api/diary/search?q={searchTerm}" token None

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode)

            use! results = readJson searchResponse
            let firstResult = results.RootElement.EnumerateArray() |> Seq.head

            Assert.Equal(noteId, firstResult.GetProperty("noteId").GetString())
            Assert.Contains(searchTerm, firstResult.GetProperty("snippet").GetString())
            Assert.True(firstResult.GetProperty("rank").GetInt32() > 0)
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
