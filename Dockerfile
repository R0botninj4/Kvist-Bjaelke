FROM oven/bun:1.3.14 AS frontend
WORKDIR /src/client
COPY client/package.json client/bun.lock ./
RUN bun install --frozen-lockfile
COPY client/ ./
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY server/DataAccess/DataAccess.csproj server/DataAccess/
COPY server/Security/Security.csproj server/Security/
RUN dotnet restore server/Security/Security.csproj
COPY server/ server/
COPY --from=frontend /src/server/Security/wwwroot/ server/Security/wwwroot/
RUN dotnet publish server/Security/Security.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app ./
RUN mkdir /data && chown app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__DefaultConnection="Data Source=/data/app.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "Security.dll"]
