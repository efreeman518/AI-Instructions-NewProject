# ═══════════════════════════════════════════════════════════════
# Pattern: Multi-stage Dockerfile — API host.
# Stage 1 (build): Restore packages, build, publish.
# Stage 2 (runtime): Chiseled base image, minimal attack surface.
# Build context: src/ (docker build -f TaskFlow/TaskFlow.Api/Dockerfile .)
# ═══════════════════════════════════════════════════════════════

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Pattern: Copy nuget.config first — contains GitHub Packages feed credentials.
COPY nuget.config .

# Pattern: Copy all .csproj files and restore — Docker layer caching optimization.
# Only re-restores when project references change, not when source code changes.
COPY Directory.Packages.props .
COPY global.json .
COPY Domain/TaskFlow.Domain.Model/TaskFlow.Domain.Model.csproj Domain/TaskFlow.Domain.Model/
COPY Domain/TaskFlow.Domain.Shared/TaskFlow.Domain.Shared.csproj Domain/TaskFlow.Domain.Shared/
COPY Application/TaskFlow.Application.Contracts/TaskFlow.Application.Contracts.csproj Application/TaskFlow.Application.Contracts/
COPY Application/TaskFlow.Application.Models/TaskFlow.Application.Models.csproj Application/TaskFlow.Application.Models/
COPY Application/TaskFlow.Application.Services/TaskFlow.Application.Services.csproj Application/TaskFlow.Application.Services/
COPY Application/TaskFlow.Application.MessageHandlers/TaskFlow.Application.MessageHandlers.csproj Application/TaskFlow.Application.MessageHandlers/
COPY Infrastructure/TaskFlow.Infrastructure.Data/TaskFlow.Infrastructure.Data.csproj Infrastructure/TaskFlow.Infrastructure.Data/
COPY Infrastructure/TaskFlow.Infrastructure.Repositories/TaskFlow.Infrastructure.Repositories.csproj Infrastructure/TaskFlow.Infrastructure.Repositories/
COPY Infrastructure/TaskFlow.Infrastructure.Notification/TaskFlow.Infrastructure.Notification.csproj Infrastructure/TaskFlow.Infrastructure.Notification/
COPY TaskFlow/TaskFlow.Bootstrapper/TaskFlow.Bootstrapper.csproj TaskFlow/TaskFlow.Bootstrapper/
COPY TaskFlow/TaskFlow.BackgroundServices/TaskFlow.BackgroundServices.csproj TaskFlow/TaskFlow.BackgroundServices/
COPY TaskFlow/TaskFlow.Api/TaskFlow.Api.csproj TaskFlow/TaskFlow.Api/
COPY Aspire/ServiceDefaults/ServiceDefaults.csproj Aspire/ServiceDefaults/

RUN dotnet restore TaskFlow/TaskFlow.Api/TaskFlow.Api.csproj

# Pattern: Copy full source and build in Release mode.
COPY . .
WORKDIR /src/TaskFlow/TaskFlow.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──
# Pattern: Chiseled image — no shell, no package manager, non-root by default.
# "extra" variant includes ICU globalization support (required for .NET culture-aware ops).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080

# Pattern: Copy published output from build stage.
COPY --from=build /app/publish .

# Pattern: Set ASP.NET Core URLs to listen on port 8080 (non-root).
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
