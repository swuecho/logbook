module AppStartup

open System
open System.Threading.Tasks
open Falco
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Npgsql

let corsPolicyName = "MyCorsPolicy"

let private publicApiPaths =
    ApiPaths.publicApiPaths |> List.map PathString

let private enabledValues =
    set [ "1"; "true"; "yes"; "on" ]

let isEnvFlagEnabled name =
    let value = Environment.GetEnvironmentVariable(name)

    if String.IsNullOrWhiteSpace(value) then
        false
    else
        enabledValues.Contains(value.Trim().ToLowerInvariant())

let initializeJwtConfig (dataSource: NpgsqlDataSource) : JwtService.JwtConfig =
    use pgConn = dataSource.OpenConnection()
    let jwtSecret = JwtService.getOrCreateJwtSecret pgConn AppIdentity.jwtAudienceName
    { Secret = jwtSecret.Secret
      Audience = jwtSecret.Audience }

let initializeDatabase (dataSource: NpgsqlDataSource) =
    Database.InitDB.init dataSource |> ignore

let initializeSearchIndex (dataSource: NpgsqlDataSource) =
    use pgConn = dataSource.OpenConnection()
    SearchIndexService.refreshSearchIndex pgConn

let corsPolicy (policyBuilder: CorsPolicyBuilder) =
    // Note: This is a very lax setting, but a good fit for local development.
    policyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin() |> ignore

let addCors (services: IServiceCollection) =
    services.AddCors(fun options -> options.AddPolicy(corsPolicyName, corsPolicy))

let addAuthentication (jwtConfig: JwtService.JwtConfig) (services: IServiceCollection) =
    services.AddSingleton<JwtService.JwtConfig>(jwtConfig) |> ignore

    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidIssuer = AppIdentity.jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtConfig.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtConfig.Secret))
                ))
    |> ignore

    services

let addDatabase dataSource services =
    Database.Connection.addDatabase dataSource services

let addSummaryBackgroundProcessing (services: IServiceCollection) =
    services.AddSingleton<SummaryBackgroundService.SummaryUpdateQueue>() |> ignore
    services.AddHostedService<SummaryBackgroundService.SummaryRefreshWorker>() |> ignore
    services

let requireAuthenticatedApiRoutes (app: IApplicationBuilder) =
    let isAuthenticated (context: HttpContext) =
        context.User.Identity <> null && context.User.Identity.IsAuthenticated

    let hasRequiredClaims (context: HttpContext) =
        HttpAuth.tryGetUserId context.User |> Option.isSome

    let isApiRequest (context: HttpContext) =
        context.Request.Path.StartsWithSegments(PathString(ApiPaths.apiPrefix))

    let isPublicApiRequest (context: HttpContext) =
        publicApiPaths
        |> List.exists (fun path -> context.Request.Path.Equals(path))

    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
        if isApiRequest context && not (isPublicApiRequest context) then
            if isAuthenticated context && hasRequiredClaims context then
                next.Invoke context
            else
                HttpAuth.unauthorized context
        else
            next.Invoke context

    app.Use(middleware)

let serveVueFiles (app: IApplicationBuilder) =
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseEndpoints(fun endpoints -> endpoints.MapFallbackToFile("/index.html") |> ignore)
