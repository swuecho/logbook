FROM  mcr.microsoft.com/dotnet/sdk:7.0-alpine3.17 AS build

# build application
WORKDIR /src
COPY . .
# RUN sed -i 's/netframework5.0/netcoreapp5.0/' Notepad.fsproj
RUN dotnet restore
RUN dotnet publish -c release -o /app --no-self-contained --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine3.17
ENV TZ=Asia/Shanghai
WORKDIR /app
# copy Resource folder 
COPY --from=build /src/Resources /src/Resources
COPY --from=build /src/wwwroot .
COPY --from=build /app .
ENTRYPOINT ["./Notepad"]
