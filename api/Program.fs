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

let getOrCreateJwtSecret pgConn jwtAudienceName =
    let getExistingSecret () =
        try
            let secret = JwtSecrets.GetJwtSecret pgConn jwtAudienceName
            printfn "Existing JWT Secret found for %s" jwtAudienceName
            Some secret
        with :? NoResultsException -> None

    let generateRandomKey () =
        System.Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))

    let getJwtKey () =
        match Util.getEnvVar "JWT_SECRET" with
        | null -> 
            let randomKey = generateRandomKey()
            printfn "Warning: JWT_SECRET not set. Using randomly generated key: %s" randomKey
            randomKey
        | key -> key

    let getAudience () =
        match Util.getEnvVar "JWT_AUDIENCE" with
        | null -> 
            let defaultAudience = "http://localhost:5000"
            printfn "Warning: JWT_AUDIENCE not set. Using default audience: %s" defaultAudience
            defaultAudience
        | aud -> aud

    let createNewSecret () =
        let jwtSecretParams: JwtSecrets.CreateJwtSecretParams = 
            { Name = jwtAudienceName
              Secret = getJwtKey()
              Audience = getAudience() }
        let createdSecret = JwtSecrets.CreateJwtSecret pgConn jwtSecretParams
        printfn "New JWT Secret created for %s" jwtAudienceName
        createdSecret

    match getExistingSecret() with
    | Some secret -> secret
    | None -> createNewSecret()

let authService (services: IServiceCollection) =
    let connectionString = Database.Config.connStr
    use pgConn = new Npgsql.NpgsqlConnection(connectionString)
    pgConn.Open()

    let jwtAudienceName = "logbook"

    let jwtSecret = getOrCreateJwtSecret pgConn jwtAudienceName
    
    pgConn.Close()

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
                        ValidAudience = jwtSecret.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret.Secret))
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
