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

let authenticateRouteMiddleware (app: IApplicationBuilder) =
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
    let jwtKey = Util.getEnvVar "JWT_SECRET"
    let audience = Util.getEnvVar "JWT_AUDIENCE"


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

// let config = configuration [||] { add_env }

webHost [||] {
    // Use the specified middleware if the provided predicate is "true".
    use_if FalcoExtensions.IsDevelopment DeveloperExceptionPageExtensions.UseDeveloperExceptionPage

    use_cors corsPolicyName corsOptions
    add_service authService

    // Use authorization middleware. Call before any middleware that depends on users being authenticated.
    // jwt decode add set context.User.Identity.IsAuthenticated true if user is valid
    use_authentication

    use_middleware stashConnteciton

    // check user is authorized
    use_middleware authenticateRouteMiddleware

    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0
    endpoints
        [ post "/api/login" Note.login
          get "/api/diary" Note.noteAllPart
          get "/api/diary/{id}" Note.noteByIdPartDebug
          put "/api/diary/{id}" Note.addNotePart ]

    use_middleware serveVueFiles
}
