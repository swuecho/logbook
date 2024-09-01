FROM  mcr.microsoft.com/dotnet/sdk:8.0-alpine3.19 AS build

# build application
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c release -o /app --no-self-contained --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.19
ENV TZ=Asia/Shanghai
WORKDIR /app
# copy Resource folder 
COPY --from=build /src/Resources /src/Resources
COPY --from=build /src/api/sql/ /src/api/sql/
COPY --from=build /src/wwwroot ./wwwroot
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["./Notepad"]
