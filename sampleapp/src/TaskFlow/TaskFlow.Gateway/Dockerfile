# ═══════════════════════════════════════════════════════════════
# Pattern: Multi-stage Dockerfile — Gateway (YARP reverse proxy).
# Lighter dependency tree than API — no EF, no domain model.
# Build context: src/ (docker build -f TaskFlow/TaskFlow.Gateway/Dockerfile .)
# ═══════════════════════════════════════════════════════════════

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config .
COPY Directory.Packages.props .
COPY global.json .

# Pattern: Gateway has minimal project references — just ServiceDefaults.
COPY TaskFlow/TaskFlow.Gateway/TaskFlow.Gateway.csproj TaskFlow/TaskFlow.Gateway/
COPY Aspire/ServiceDefaults/ServiceDefaults.csproj Aspire/ServiceDefaults/

RUN dotnet restore TaskFlow/TaskFlow.Gateway/TaskFlow.Gateway.csproj

COPY . .
WORKDIR /src/TaskFlow/TaskFlow.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TaskFlow.Gateway.dll"]
