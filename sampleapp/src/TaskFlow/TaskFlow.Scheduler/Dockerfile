# ═══════════════════════════════════════════════════════════════
# Pattern: Multi-stage Dockerfile — Scheduler (TickerQ background jobs).
# Similar dependency tree to API but without API-specific concerns.
# Build context: src/ (docker build -f TaskFlow/TaskFlow.Scheduler/Dockerfile .)
# ═══════════════════════════════════════════════════════════════

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config .
COPY Directory.Packages.props .
COPY global.json .

# Pattern: Scheduler shares the same dependency graph as API minus API-specific projects.
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
COPY TaskFlow/TaskFlow.Scheduler/TaskFlow.Scheduler.csproj TaskFlow/TaskFlow.Scheduler/
COPY Aspire/ServiceDefaults/ServiceDefaults.csproj Aspire/ServiceDefaults/

RUN dotnet restore TaskFlow/TaskFlow.Scheduler/TaskFlow.Scheduler.csproj

COPY . .
WORKDIR /src/TaskFlow/TaskFlow.Scheduler
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TaskFlow.Scheduler.dll"]
