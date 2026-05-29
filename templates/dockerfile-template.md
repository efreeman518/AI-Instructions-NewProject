# Dockerfile Template

| | |
|---|---|
| **File** | `src/Host/{Host}.Api/Dockerfile` (or per-host project root under `Host/`) |
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
COPY src/Host/{Host}.Bootstrapper/{Host}.Bootstrapper.csproj src/Host/{Host}.Bootstrapper/
COPY src/Host/{Host}.Api/{Host}.Api.csproj src/Host/{Host}.Api/

RUN dotnet restore src/Host/{Host}.Api/{Host}.Api.csproj

# ===== Stage 2: Build + Publish =====
FROM restore AS publish
COPY . .
RUN dotnet publish src/Host/{Host}.Api/{Host}.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ===== Stage 3: Runtime (chiseled, non-root) =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Health probes - configure at orchestrator level (Container Apps / Kubernetes),
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
# Isolated worker listens on port 80 (image sets no ASPNETCORE_URLS).
EXPOSE 80
```

> **Functions port is 80, not 8080.** The dotnet-isolated base image listens on **80**; do not add `EXPOSE 8080` or set `ASPNETCORE_URLS=...:8080` for Functions. On Container Apps the Functions app's `--target-port`/ingress must be 80. This differs from the ASP.NET hosts above, which use 8080. See [skills/function-app.md](../skills/function-app.md) -> *Container Port on ACA*.

## Build Context

This template's `COPY` paths reference both repo-root files (`Directory.Packages.props`, `{SolutionName}.slnx`) and `src/...` paths, so it is built with the **repo root** as context:

```bash
docker build -f src/Host/{Host}.Api/Dockerfile .
```

The build context must match the Dockerfile's `COPY` roots. If you rewrite `COPY` lines to be `src/`-relative, build from `src/` instead. Keep the CI build context ([skills/cicd.md](../skills/cicd.md)) aligned with whichever rooting this Dockerfile uses - do not assume `src/`.

## Rules

- **Always use chiseled base images** (`-noble-chiseled-extra`) for production - smaller attack surface, no shell.
- **Restore layer caching:** Copy `.csproj` files first, then `dotnet restore`, then copy source. This ensures source changes don't invalidate the restore cache.
- **Port:** Default to `8080` for ASP.NET hosts (API/Gateway/Scheduler) on Container Apps. **Exception: Azure Functions isolated worker listens on 80** - see the Function App variant above.
- **Non-root:** Chiseled images run as non-root by default.
- **Health check:** Match the path configured in `Program.cs` (`/health` or `/alive`).
- **No secrets in image:** Use Aspire/Container Apps environment injection for connection strings.
- Adjust COPY lines to match your actual solution project structure - add or remove projects as needed.

## Verification Checklist

- [ ] Restore stage copies all `.csproj` files needed by the target host
- [ ] `Directory.Packages.props`, `nuget.config`, and `global.json` are copied before restore
- [ ] Publish uses `--no-restore` (relies on cached restore layer)
- [ ] Runtime uses chiseled non-root base image
- [ ] `EXPOSE` port matches Container Apps / Aspire configuration
- [ ] `ENTRYPOINT` matches the published assembly name
- [ ] No `HEALTHCHECK` in image - health probes configured at orchestrator level (Container Apps / K8s)
