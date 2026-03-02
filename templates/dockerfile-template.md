# Dockerfile Template

| | |
|---|---|
| **File** | `src/{Host}/{Host}.Api/Dockerfile` (or per-host project root) |
| **Depends on** | [skills/cicd.md](../skills/cicd.md), [skills/iac.md](../skills/iac.md) |
| **Referenced by** | [skills/aspire.md](../skills/aspire.md) |

## Multi-Stage Chiseled Pattern

Use this pattern for all host projects (API, Gateway, Scheduler, Functions).

```dockerfile
# ===== Stage 1: Restore (cached layer) =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy only project files + central package management for restore cache
COPY Directory.Packages.props .
COPY nuget.config .
COPY global.json .
COPY {SolutionName}.slnx .

# Copy all .csproj files preserving folder structure
COPY src/Domain/{Project}.Domain.Model/{Project}.Domain.Model.csproj src/Domain/{Project}.Domain.Model/
COPY src/Application/{Project}.Application.Models/{Project}.Application.Models.csproj src/Application/{Project}.Application.Models/
COPY src/Application/{Project}.Application.Contracts/{Project}.Application.Contracts.csproj src/Application/{Project}.Application.Contracts/
COPY src/Application/{Project}.Application.Mappers/{Project}.Application.Mappers.csproj src/Application/{Project}.Application.Mappers/
COPY src/Application/{Project}.Application.Services/{Project}.Application.Services.csproj src/Application/{Project}.Application.Services/
COPY src/Infrastructure/{Project}.Infrastructure.Data/{Project}.Infrastructure.Data.csproj src/Infrastructure/{Project}.Infrastructure.Data/
COPY src/Infrastructure/{Project}.Infrastructure.Repositories/{Project}.Infrastructure.Repositories.csproj src/Infrastructure/{Project}.Infrastructure.Repositories/
COPY src/{Host}/{Host}.Bootstrapper/{Host}.Bootstrapper.csproj src/{Host}/{Host}.Bootstrapper/
COPY src/{Host}/{Host}.Api/{Host}.Api.csproj src/{Host}/{Host}.Api/

RUN dotnet restore src/{Host}/{Host}.Api/{Host}.Api.csproj

# ===== Stage 2: Build + Publish =====
FROM restore AS publish
COPY . .
RUN dotnet publish src/{Host}/{Host}.Api/{Host}.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ===== Stage 3: Runtime (chiseled, non-root) =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Health probes — configure at orchestrator level (Container Apps / Kubernetes),
# not via HEALTHCHECK directive (chiseled images have no shell/curl).
# See skills/aspire.md for Container Apps health probe configuration.

EXPOSE 8080
ENTRYPOINT ["dotnet", "{Host}.Api.dll"]
```

## Variant: Gateway

Same pattern but replace `{Host}.Api` with `{Gateway}.Gateway` and include YARP dependencies.

## Variant: Scheduler

Same pattern but replace `{Host}.Api` with `{Host}.Scheduler` and include TickerQ dependencies. Add `--replicas=1` in orchestration.

## Variant: Function App

```dockerfile
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated9.0 AS runtime
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
```

## Rules

- **Always use chiseled base images** (`-noble-chiseled-extra`) for production — smaller attack surface, no shell.
- **Restore layer caching:** Copy `.csproj` files first, then `dotnet restore`, then copy source. This ensures source changes don't invalidate the restore cache.
- **Port:** Default to `8080` for Container Apps compatibility.
- **Non-root:** Chiseled images run as non-root by default.
- **Health check:** Match the path configured in `Program.cs` (`/health` or `/alive`).
- **No secrets in image:** Use Aspire/Container Apps environment injection for connection strings.
- Adjust COPY lines to match your actual solution project structure — add or remove projects as needed.

## Verification Checklist

- [ ] Restore stage copies all `.csproj` files needed by the target host
- [ ] `Directory.Packages.props`, `nuget.config`, and `global.json` are copied before restore
- [ ] Publish uses `--no-restore` (relies on cached restore layer)
- [ ] Runtime uses chiseled non-root base image
- [ ] `EXPOSE` port matches Container Apps / Aspire configuration
- [ ] `ENTRYPOINT` matches the published assembly name
- [ ] No `HEALTHCHECK` in image — health probes configured at orchestrator level (Container Apps / K8s)
