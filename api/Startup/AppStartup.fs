module AppStartup

open System.Threading.Tasks
open Falco
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens

let jwtAudienceName = "logbook"
let corsPolicyName = "MyCorsPolicy"

let initializeJwtConfig () : JwtService.JwtConfig =
    use pgConn = new Npgsql.NpgsqlConnection(Database.Config.connStr)
    pgConn.Open()

    let jwtSecret = JwtService.getOrCreateJwtSecret pgConn jwtAudienceName
    { Secret = jwtSecret.Secret
      Audience = jwtSecret.Audience }

let initializeDatabase () =
    Database.InitDB.init Database.Config.connStr |> ignore

let initializeSearchIndex () =
    use pgConn = new Npgsql.NpgsqlConnection(Database.Config.connStr)
    pgConn.Open()
    SearchIndexService.refreshSearchIndex pgConn

let corsPolicy (policyBuilder: CorsPolicyBuilder) =
    // Note: This is a very lax setting, but a good fit for local development.
    policyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin() |> ignore

let addCors (services: IServiceCollection) =
    services.AddCors(fun options -> options.AddPolicy(corsPolicyName, corsPolicy))

let addAuthentication (jwtConfig: JwtService.JwtConfig) (services: IServiceCollection) =
    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidateLifetime = true,
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidAudience = jwtConfig.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtConfig.Secret))
                ))
    |> ignore

    services

let usePerRequestConnection (app: IApplicationBuilder) =
    app.Use(Database.Connection.UseNpgsqlConnectionMiddleware Database.Config.connStr)

let requireAuthenticatedApiRoutes (app: IApplicationBuilder) =
    let isAuthenticated (context: HttpContext) =
        context.User.Identity <> null && context.User.Identity.IsAuthenticated

    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
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

let serveVueFiles (app: IApplicationBuilder) =
    app.UseRouting() |> ignore
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseEndpoints(fun endpoints -> endpoints.MapFallbackToFile("/index.html") |> ignore)
