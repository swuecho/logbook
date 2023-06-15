open Falco
open Falco.Routing
open Falco.HostBuilder

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.Http
open System.Threading.Tasks

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
    app.UseEndpoints(fun endpoints -> endpoints.MapFallbackToFile("/index.html") |> ignore)


let stashConnteciton (app: IApplicationBuilder) =
    app.Use(Database.Connection.UseNpgsqlConnectionMiddleware Database.Config.connStr)

let authUserMiddleware (app: IApplicationBuilder) =
    let isAuthenticated (context: HttpContext) =
        context.User.Identity.IsAuthenticated = true

    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
        // check path start with /api
        printfn "%A" context.Request.Path

        if
            context.Request.Path.StartsWithSegments(PathString("/api"))
            && context.Request.Path.ToString() <> "/api/login"
        then
            if isAuthenticated context then
                next.Invoke context
            else
                context.Response.StatusCode <- 401
                context.Response.WriteAsync "Unauthorized"
        else
            next.Invoke context

    app.Use(middleware)


let authService (services: IServiceCollection) =
    let jwtKey = "Uv38ByGCZU8WP18PmmIdcpVmx00QA3xNe7sEB9Hixkk="
    let audience = "gDsc2WD8F2qNfHK5a84jjJkwzDkh9h2f"

    let _ =
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.TokenValidationParameters <-
                    new TokenValidationParameters(
                        ValidateLifetime = true,
                        ValidateIssuer = false,
                        //ValidIssuer = Configuratio["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
                    ))

    services

webHost [||] {
    use_if FalcoExtensions.IsDevelopment DeveloperExceptionPageExtensions.UseDeveloperExceptionPage
    use_cors corsPolicyName corsOptions
    add_service authService
    use_authentication

    use_middleware stashConnteciton

    use_middleware authUserMiddleware

    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0
    endpoints
        [ post "/api/login" Note.login
          get "/api/diary" Note.noteAllPart
          get "/api/diary/{id}" Note.noteByIdPartDebug
          put "/api/diary" Note.addNotePart ]

    use_middleware serveVueFiles
}
