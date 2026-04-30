open Falco
open Microsoft.AspNetCore.Builder

let dataSource = Database.Connection.createDataSource Database.Config.connStr
UserRevocationCache.initialize dataSource

let jwtConfig = AppStartup.initializeJwtConfig dataSource

let builder = WebApplication.CreateBuilder()
builder.Services |> AppStartup.addDatabase dataSource |> ignore
builder.Services |> AppStartup.addAuthentication jwtConfig |> ignore
builder.Services |> AppStartup.addSummaryBackgroundProcessing |> ignore
builder.Services |> AppStartup.addIndexBackgroundProcessing |> ignore
builder.Services |> AppStartup.addApplicationServices |> ignore
builder.Services |> AppStartup.addBackgroundJobsWorker |> ignore
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
