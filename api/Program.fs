open Falco
open Microsoft.AspNetCore.Builder

let dataSource = Database.Connection.createDataSource Database.Config.connStr

if AppStartup.isEnvFlagEnabled "LOGBOOK_RUN_SCHEMA_INIT_ON_STARTUP" then
    AppStartup.initializeDatabase dataSource

let jwtConfig = AppStartup.initializeJwtConfig dataSource

if AppStartup.isEnvFlagEnabled "LOGBOOK_REFRESH_SEARCH_INDEX_ON_STARTUP" then
    AppStartup.initializeSearchIndex dataSource

let builder = WebApplication.CreateBuilder()
builder.Services |> AppStartup.addDatabase dataSource |> ignore
builder.Services |> AppStartup.addAuthentication jwtConfig |> ignore
builder.Services |> AppStartup.addSummaryBackgroundProcessing |> ignore
builder.Services |> AppStartup.addCors |> ignore

let wapp = builder.Build()
let isDevelopment = wapp.Environment.EnvironmentName = "Development"

wapp.UseRouting()
    .UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
    .UseCors(AppStartup.corsPolicyName)
    .UseAuthentication()
    .Use(RequestLogging.useMiddleware)
    .Use(AppStartup.requireAuthenticatedApiRoutes)
    .UseFalco(ApiRoutes.endpoints)
    .Use(AppStartup.serveVueFiles)
    |> ignore

wapp.Run()
