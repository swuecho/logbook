module AppStartup

open System
open System.Threading.Tasks
open System.Security.Claims
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

let corsPolicy (policyBuilder: CorsPolicyBuilder) =
    // The web app uses same-origin API requests, including through the dev proxy.
    policyBuilder.WithOrigins([||]) |> ignore

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
                    ClockSkew = TimeSpan.Zero,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [| SecurityAlgorithms.HmacSha256 |],
                    RoleClaimType = AppIdentity.roleClaim,
                    ValidateIssuer = true,
                    ValidIssuer = AppIdentity.jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtConfig.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtConfig.Secret))
                )
            options.MapInboundClaims <- false
            options.Events <- JwtBearerEvents(
                OnMessageReceived = (fun ctx ->
                    let token = SessionHttp.cookie ctx.HttpContext
                    if token = "" then ctx.NoResult() else ctx.Token <- token
                    Task.CompletedTask),
                OnTokenValidated = (fun ctx ->
                    let db = ctx.HttpContext.RequestServices.GetRequiredService<Database.DbSession>()
                    let token = SessionHttp.cookie ctx.HttpContext
                    match HttpAuth.tryGetUserId ctx.Principal with
                    | Some userId ->
                        match SessionService.authenticate db jwtConfig token userId with
                        | Some session ->
                            SessionHttp.setSession ctx.HttpContext session
                            // Read privileges from the account, never a stale role in a JWT.
                            ctx.Principal <- ClaimsPrincipal(ClaimsIdentity(
                                [ Claim(AppIdentity.userIdClaim, string session.UserId); Claim(AppIdentity.roleClaim, session.Role) ],
                                JwtBearerDefaults.AuthenticationScheme, AppIdentity.userIdClaim, AppIdentity.roleClaim))
                        | None -> ctx.Fail("Session expired or revoked.")
                    | None -> ctx.Fail("Invalid user claim.")
                    Task.CompletedTask)))
    |> ignore

    services

let addDatabase dataSource services =
    Database.Connection.addDatabase dataSource services

let addSummaryBackgroundProcessing (services: IServiceCollection) =
    services.AddSingleton<SummaryQueue.SummaryUpdateQueue>() |> ignore
    services

let addIndexBackgroundProcessing (services: IServiceCollection) =
    services.AddSingleton<IndexQueue.IndexUpdateQueue>() |> ignore
    services

let addApplicationServices (services: IServiceCollection) =
    services.AddSingleton<ApplicationContracts.IBackgroundJobPublisher, DiaryService.QueueBackedBackgroundJobPublisher>()
    |> ignore

    services.AddSingleton<ApplicationContracts.IBackgroundMaintenanceService, BackgroundMaintenanceService.BackgroundMaintenanceService>()
    |> ignore

    services

let addBackgroundJobsWorker (services: IServiceCollection) =
    services.AddHostedService<BackgroundJobsService.Worker>() |> ignore
    services

let requireAuthenticatedApiRoutes (app: IApplicationBuilder) =
    let middleware (context: HttpContext) (next: RequestDelegate) : Task =
        if not (context.Request.Path.StartsWithSegments(PathString(ApiPaths.apiPrefix))) then next.Invoke context
        else
            context.Response.Headers.CacheControl <- "no-store"
            context.Response.Headers["X-Content-Type-Options"] <- "nosniff"
            let publicPath = publicApiPaths |> List.exists (fun path -> context.Request.Path.Equals(path))
            let isSessionRead = context.Request.Path.Equals(PathString(ApiPaths.session)) && HttpMethods.IsGet(context.Request.Method)
            if not (SessionHttp.originAllowed context) then
                HandlerResponse.jsonWithStatus 403 {| code = "origin_rejected"; message = "Request origin is not allowed." |} context
            elif not publicPath && (SessionHttp.current context |> Option.isNone) then HttpAuth.unauthorized context
            // Bind reads as well as writes to the browser's captured session. A
            // changed cookie cannot retarget an old tab's requests to another user.
            elif not isSessionRead && not (SessionToken.equals (SessionHttp.expectedCsrf context) (context.Request.Headers[SessionToken.csrfHeader].ToString())) then
                HandlerResponse.jsonWithStatus 403 {| code = "session_changed"; message = "Session changed. Reload your session before retrying." |} context
            else next.Invoke context
    app.Use middleware

let serveVueFiles (app: IApplicationBuilder) =
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseEndpoints(fun endpoints -> endpoints.MapFallbackToFile("/index.html") |> ignore)
