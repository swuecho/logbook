# F# project

# project structure

*.fsproj: This is your project file, defining project settings, dependencies, and build configurations.

Program.fs: This is the main entry point of your F# ASP.NET Core application

# develop

```sh
dotnet restore
dotnet build
dotnet watch run
```

## Port

in prod, default port is 8080 (aspnet 8), 80 (aspnet < 8).
in dev, default port is 500
