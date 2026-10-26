FROM oven/bun:1.3.14 AS frontend
WORKDIR /src/client
COPY client/ ./
RUN bun install --frozen-lockfile && bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/ server/
COPY --from=frontend /src/server/Security/wwwroot/ server/Security/wwwroot/
RUN dotnet publish server/Security/Security.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
RUN mkdir /data && chown app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__DefaultConnection="Data Source=/data/app.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "Security.dll"]
