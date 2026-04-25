open Falco
open Microsoft.AspNetCore.Builder

AppStartup.initializeDatabase()
let jwtConfig = AppStartup.initializeJwtConfig()
AppStartup.initializeSearchIndex()

let builder = WebApplication.CreateBuilder()
builder.Services |> AppStartup.addAuthentication jwtConfig |> ignore
builder.Services |> AppStartup.addCors |> ignore

let wapp = builder.Build()
let isDevelopment = wapp.Environment.EnvironmentName = "Development"

wapp.UseRouting()
    .UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
    .UseCors(AppStartup.corsPolicyName)
    .UseAuthentication()
    .Use(AppStartup.usePerRequestConnection)
    .Use(AppStartup.requireAuthenticatedApiRoutes)
    .UseFalco(ApiRoutes.endpoints)
    .Use(AppStartup.serveVueFiles)
    |> ignore

wapp.Run()
