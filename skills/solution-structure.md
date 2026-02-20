# Solution Structure

## Overview

The solution follows **clean architecture** with a shared Bootstrapper pattern that enables the same domain/application/infrastructure code to be reused across multiple deployable hosts (API, Functions, Scheduler, Tests).

## Folder Layout

> **Reference implementation:** See `sampleapp/src/` for the complete 25-project solution demonstrating this layout.

```
src/
├── Domain/
│   ├── {Project}.Domain.Model/          # Entities, value objects, aggregate roots
│   ├── {Project}.Domain.Shared/         # Enums, [Flags], exceptions, constants
│   └── {Project}.Domain.Rules/          # Specification pattern rules
├── Application/
│   ├── {Project}.Application.Contracts/ # Interfaces, static mappers, events, constants
│   ├── {Project}.Application.Models/    # DTOs (records), search filters
│   ├── {Project}.Application.Services/  # Service implementations, validation rules
│   └── {Project}.Application.MessageHandlers/  # Internal event/message handlers
├── Infrastructure/
│   ├── {Project}.Infrastructure.Data/          # DbContexts, EF configurations, migrations
│   ├── {Project}.Infrastructure.Repositories/  # Repository implementations, Updaters/
│   └── {Project}.Infrastructure.{ExternalService}/  # External service integrations
├── {Host}.Bootstrapper/          # Centralized DI registration hub
├── {Host}.Api/                   # ASP.NET Core Minimal API + Auth/ + HealthChecks/ + Dockerfile
├── {Host}.Scheduler/             # Background job scheduler (optional)
├── {Host}.BackgroundServices/    # Hosted services, channel-based queues
├── {Gateway}.Gateway/            # YARP reverse proxy + Auth/ + StartupTasks/ + Dockerfile
├── {Host}.UI/                    # Uno Platform MVUX cross-platform UI (optional)
├── Functions/
│   └── FunctionApp/              # Azure Functions (optional)
├── Aspire/
│   ├── AppHost/                  # .NET Aspire orchestration
│   └── ServiceDefaults/          # Shared telemetry, resilience, health
├── Test/
│   ├── Test.Unit/                # MSTest + Moq unit tests
│   ├── Test.Integration/         # WebApplicationFactory integration tests
│   ├── Test.Architecture/        # Dependency & convention rules (NetArchTest)
│   ├── Test.PlaywrightUI/        # E2E browser tests (Playwright)
│   ├── Test.Load/                # Load/stress testing (NBomber)
│   ├── Test.Benchmarks/          # Micro-benchmarks (BenchmarkDotNet)
│   └── Test.Support/             # Shared test utilities
├── Directory.Packages.props      # Central Package Management
├── global.json                   # SDK version pinning
├── nuget.config                  # NuGet sources
└── {SolutionName}.slnx           # Solution file
```

## Project Reference Rules (Dependency Flow)

```
Domain.Model ← Domain.Shared (+ Package.Infrastructure.Domain for EntityBase)
Domain.Rules ← Domain.Model, Domain.Shared
     ↑
Application.Contracts ← Application.Models, Domain.Model
     ↑
Application.Services ← Application.Contracts, Application.Models
     ↑
Infrastructure.Data ← Domain.Model (for entity types)
Infrastructure.Repositories ← Application.Contracts, Infrastructure.Data
     ↑
Bootstrapper ← Application.Services, Application.MessageHandlers,
                Infrastructure.Repositories, Infrastructure.{Services}
     ↑
Api ← Bootstrapper (+ API-specific: auth, endpoints, middleware)
FunctionApp ← Bootstrapper
Scheduler ← Bootstrapper
Test.Unit ← Application.Services (direct, no Bootstrapper needed)
Test.Integration ← Api (via WebApplicationFactory)
```

**Critical rule:** Domain.Model and Domain.Shared must NEVER reference Application or Infrastructure projects.

> **Optional host projects:** `{Host}.Scheduler`, `{Host}.BackgroundServices`, and `Functions/FunctionApp` are optional deployable hosts. They follow the same Bootstrapper pattern as the API — reference `{Host}.Bootstrapper` for DI and add only host-specific configuration. For Scheduler and Functions, use their dedicated skill files: [background-services.md](background-services.md) and [function-app.md](function-app.md). For custom workers beyond those patterns, start from `bootstrapper.md` + `api.md` conventions.

## global.json

> **Version policy:** Always target the **latest stable** .NET SDK. Use `"rollForward": "latestFeature"` to auto-advance within the major version. Do not hardcode specific versions — run `dotnet --version` to detect what’s installed.
>
> The `version` value below is an example format. Replace it with the SDK version installed on your machine.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

## Directory.Packages.props

Use Central Package Management. All NuGet versions are declared here, individual `.csproj` files use `<PackageReference Include="..." />` without version.

> **Reference implementation:** See `sampleapp/src/Directory.Packages.props` for a complete example with ~55 packages covering EF Core, ASP.NET, Identity, Caching, Azure, YARP, Aspire, Testing, and Package.Infrastructure.* internal packages.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.x" />
    <PackageVersion Include="ZiggyCreatures.FusionCache" Version="2.x" />
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.x" />
    <PackageVersion Include="MSTest" Version="3.x" />
    <!-- See sampleapp/src/Directory.Packages.props for full list -->
  </ItemGroup>
</Project>
```

## .csproj Conventions

Projects should target the latest stable TFM that matches the installed SDK (for example, `net10.0`) with implicit usings and nullable enabled:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

API and Gateway projects use `Microsoft.NET.Sdk.Web`.

## Namespace Conventions

- Use one namespace style consistently across the solution:
  - **Portable layer style:** `Domain.Model`, `Application.Services`, `Infrastructure.Repositories`
  - **Project-prefixed style:** `{Project}.Domain.Model` or `{Org}.{Project}.Domain.Model`
- If `OrganizationName` is provided, prefer project-prefixed style for host-facing projects to avoid collisions.
- Entity-specific files use the root namespace (no extra sub-namespaces unless the folder is functionally distinct like `Rules/`, `Mapping/`, `Updaters/`).

## Creating the Solution

> **Reference implementation:** See `sampleapp/src/TaskFlow.slnx` for the complete .slnx structure with 25 projects in 7 solution folders.

> **IMPORTANT — `.slnx` format:** This solution uses the modern XML-based `.slnx` solution format, NOT the legacy `.sln` format. The `.slnx` file is a simple XML document with `<Solution>`, `<Folder>`, and `<Project>` elements. See the reference implementation for the exact structure. **Do NOT generate or reference any `.sln` file.**

### Option A: Hand-author the `.slnx` directly (preferred)

Because `.slnx` is simple XML, the fastest approach is to create the `{SolutionName}.slnx` file directly. Use the reference implementation (`sampleapp/src/TaskFlow.slnx`) as a template — replace project names/paths with the target project's values. This avoids any CLI confusion with legacy `.sln` files.

### Option B: Use CLI scaffolding then migrate

If using `dotnet` CLI to scaffold, be aware it initially creates a legacy `.sln`:

```bash
# Step 1 — scaffold (creates .sln — this is intermediate, NOT the final format)
dotnet new sln -n {SolutionName}

# Step 2 — create projects
dotnet new classlib -n Domain.Model -o Domain/Domain.Model
dotnet new classlib -n Domain.Shared -o Domain/Domain.Shared
dotnet new classlib -n Application.Contracts -o Application/Application.Contracts
dotnet new classlib -n Application.Services -o Application/Application.Services
dotnet new web -n {Host}.Api -o {Host}/{Host}.Api
dotnet new web -n {Gateway}.Gateway -o {Gateway}/{Gateway}.Gateway
dotnet new mstest -n Test.Unit -o Test/Test.Unit
# ... add remaining projects per folder layout above

# Step 3 — add projects to the solution
dotnet sln add **/*.csproj

# Step 4 — REQUIRED: convert the legacy .sln to .slnx
dotnet sln migrate
# This creates {SolutionName}.slnx and removes the .sln file.
# Verify the .slnx file exists and the .sln is gone before proceeding.
```

Then add project references following the dependency flow above.

---

## Infrastructure Project Naming

Infrastructure projects that integrate with external services follow a consistent naming convention:

```
Infrastructure/
├── {Project}.Infrastructure.Data/              # EF Core DbContexts, configurations, migrations
├── {Project}.Infrastructure.Repositories/      # Repository implementations
├── {Project}.Infrastructure.Utility/           # Cross-cutting utilities (timezone, etc.)
├── {Project}.Infrastructure.EntraExt/          # Entra External ID integration (public-facing identity)
├── {Project}.Infrastructure.Notification/      # Multi-channel notification providers (email, SMS, push)
├── {Project}.Infrastructure.{ServiceName}/     # Additional external integrations (per service)
```

**Naming rules:**
- One project per external service integration (e.g., `Infrastructure.EntraExt`, `Infrastructure.Notification`)
- The project name suffix describes the external service, not the internal concern
- All infrastructure projects expose interfaces in `Application.Contracts` — implementations live in the infrastructure project
- Each infrastructure project is registered in the Bootstrapper via its own extension method (e.g., `RegisterNotificationServices(config)`)

---

## Project Reference Map

This reference map shows the dependency flow as a lookup table. Use it to verify project references are correct:

```yaml
# Project → References (direct dependencies only)
Domain.Model:
  - Domain.Shared
  - Package.Infrastructure.Domain

Domain.Shared:
  - (none — leaf node)

Domain.Rules:
  - Domain.Model
  - Domain.Shared

Application.Contracts:
  - Application.Models
  - Domain.Model
  - Domain.Shared
  - Package.Infrastructure.Domain.Contracts

Application.Models:
  - Package.Infrastructure.Common

Application.Services:
  - Application.Contracts
  - Application.Models
  - Domain.Model
  - Domain.Shared
  - Package.Infrastructure.Common

Application.MessageHandlers:
  - Application.Contracts
  - Package.Infrastructure.Common

Infrastructure.Data:
  - Domain.Model
  - Domain.Shared
  - Package.Infrastructure.Data

Infrastructure.Repositories:
  - Application.Contracts
  - Application.Models
  - Infrastructure.Data
  - Package.Infrastructure.Data

Infrastructure.Utility:
  - Package.Infrastructure.Common

Infrastructure.EntraExt:
  - Application.Contracts
  - Package.Infrastructure.Common

Infrastructure.Notification:
  - Application.Contracts
  - Package.Infrastructure.Common

"{Host}.Bootstrapper":
  - Application.Services
  - Application.MessageHandlers
  - Infrastructure.Repositories
  - Infrastructure.Utility
  - Infrastructure.EntraExt       # (if identity enabled)
  - Infrastructure.Notification   # (if notifications enabled)
  - Package.Infrastructure.Host

"{Host}.Api":
  - "{Host}.Bootstrapper"
  - Aspire.ServiceDefaults        # (if Aspire enabled)

"{Host}.Scheduler":
  - "{Host}.Bootstrapper"
  - Application.Contracts
  - Aspire.ServiceDefaults        # (if Aspire enabled)

"{Gateway}.Gateway":
  - Aspire.ServiceDefaults        # (if Aspire enabled)

"Test.Unit":
  - Application.Services
  - Application.Contracts
  - Application.Models
  - Domain.Model
  - Infrastructure.Repositories
  - Infrastructure.Data
  - Test.Support

"Test.Integration":
  - "{Host}.Api"                  # (via WebApplicationFactory)
  - Test.Support

"Test.Support":
  - Application.Models
  - Domain.Model
  - Infrastructure.Data
```

---

## Solution README

Every scaffolded solution should include a `README.md` at the `src/` root.

> **Reference implementation:** See `sampleapp/README.md` for a complete example covering prerequisites, architecture overview, project map, running tests, and deployment links.

---

## Verification

After creating the solution structure, verify:

- [ ] `.slnx` file lists all projects and builds with `dotnet build`
- [ ] `Directory.Packages.props` exists and all `.csproj` files use `<PackageReference>` without version
- [ ] `global.json` exists with `rollForward: latestFeature`
- [ ] `nuget.config` includes the private NuGet source for `Package.Infrastructure.*` packages
- [ ] `nuget.config` includes `nuget.org` and all custom NuGet feeds specified in `customNugetFeeds`
- [ ] All packages in `Directory.Packages.props` are at the **latest stable versions** from nuget.org and custom feeds
- [ ] Project references follow the dependency flow (Domain → Application → Infrastructure → Bootstrapper → Host)
- [ ] Domain projects do NOT reference Application or Infrastructure
- [ ] All library projects target the latest stable TFM with `ImplicitUsings` and `Nullable` enabled
- [ ] Host projects (API, Gateway) use `Microsoft.NET.Sdk.Web`
- [ ] `README.md` exists at `src/` root with getting-started instructions
