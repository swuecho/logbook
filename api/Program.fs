open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.Http
open System.Threading.Tasks


let mutable globalJwtConfig: JwtService.JwtConfig option = None

let initializeJwtConfig () =
    let connectionString = Database.Config.connStr
    use pgConn = new Npgsql.NpgsqlConnection(connectionString)
    pgConn.Open()
    
    let jwtAudienceName = "logbook"
    let jwtSecret = JwtService.getOrCreateJwtSecret pgConn jwtAudienceName
    pgConn.Close()
    
    globalJwtConfig <- Some { Secret = jwtSecret.Secret; Audience = jwtSecret.Audience }

// Initialize JWT config at startup or fetch from db
initializeJwtConfig()

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
    match globalJwtConfig with
    | Some config ->
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.TokenValidationParameters <-
                    new TokenValidationParameters(
                        ValidateLifetime = true,
                        ValidateIssuer = false,
                        ValidateAudience = true,
                        ValidAudience = config.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =
                            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config.Secret))
                    ))
        |> ignore
    | None -> failwith "JWT configuration not initialized"
    
    services

// init db
Database.InitDB.init Database.Config.connStr |> ignore

let endpoints =
    [
        post "/api/login" Note.login
        post "/api/logout" Note.logout
        get "/api/diray_ids" Note.listDiaryIds
        get "/api/users/with-diary" Note.getUsersWithDiaryCount
        get "/api/diary" Note.noteAllPart
        get "/api/diary/{id}" Note.noteByIdPartDebug
        put "/api/diary/{id}" Note.addNotePart
        get "/api/todo" Note.todoListsHandler
        // export api
        post "/api/export_json" Note.exportDiary
        post "/api/export_md" Note.exportDiaryMarkdown
        get "/api/export_all" Note.exportAllDiaries
    ]

let builder = WebApplication.CreateBuilder()
builder.Services |> authService |> ignore
builder.Services.AddCors corsOptions |> ignore

let wapp = builder.Build()
let isDevelopment = wapp.Environment.EnvironmentName = "Development"

wapp.UseRouting()
    .UseIf(isDevelopment, DeveloperExceptionPageExtensions.UseDeveloperExceptionPage)
    .UseCors(corsPolicyName)
    .UseAuthentication()
    .Use(stashConnteciton)
    .Use(authenticateRouteMiddleware)
    .UseFalco(endpoints)
    .Use(serveVueFiles)
    |> ignore

wapp.Run()
