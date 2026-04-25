FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# build application
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c release -o /app --no-self-contained --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
ENV TZ=Asia/Shanghai
WORKDIR /app
# copy Resource folder
COPY --from=build /src/Resources /src/Resources
COPY --from=build /src/sql/ /src/sql/
COPY --from=build /src/wwwroot ./wwwroot
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["./Logbook"]
