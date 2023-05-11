open Falco
open Falco.Routing
open Falco.HostBuilder

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure

let corsPolicyName = "MyCorsPolicy"

let corsPolicy (policyBuilder: CorsPolicyBuilder) =
    // Note: This is a very lax setting, but a good fit for local development
    policyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()
    // Note: The URLs must not end with a /
    |> ignore

let corsOptions (options: CorsOptions) =
    options.AddPolicy(corsPolicyName, corsPolicy)


let serveVueFiles (app: IApplicationBuilder) =
    app.UseRouting() |> ignore
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseEndpoints(fun endpoints ->
        endpoints.MapFallbackToFile("/index.html") |> ignore)


webHost [||] {
    use_if FalcoExtensions.IsDevelopment DeveloperExceptionPageExtensions.UseDeveloperExceptionPage
    use_cors corsPolicyName corsOptions

    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0
    endpoints
        [ get "/api/diary" Service.Note.noteAllPart
          get "/api/diary/{id}" Service.Note.noteByIdPart
          put "/api/diary/{id}" Service.Note.addNotePart ]
    use_middleware serveVueFiles
}
