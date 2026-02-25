# Solution Structure

## Purpose

Define the canonical clean-architecture layout and dependency direction used by all hosts (API, Scheduler, Functions, Gateway, UI) through a shared Bootstrapper.

## Non-Negotiables

1. Use `.slnx` as the solution format (not legacy `.sln`).
2. Maintain dependency flow: Domain -> Application -> Infrastructure -> Bootstrapper -> Hosts.
3. Domain projects never reference Application or Infrastructure.
4. Use central package management via `Directory.Packages.props`.
5. Host projects add host-specific wiring only; shared registrations stay in Bootstrapper.

---

## Canonical Folder Layout

```
src/
├── Domain/
│   ├── {Project}.Domain.Model/
│   └── {Project}.Domain.Shared/
├── Application/
│   ├── {Project}.Application.Contracts/
│   ├── {Project}.Application.Models/
│   ├── {Project}.Application.Services/
│   └── {Project}.Application.MessageHandlers/
├── Infrastructure/
│   ├── {Project}.Infrastructure.Data/
│   ├── {Project}.Infrastructure.Repositories/
│   └── {Project}.Infrastructure.{ServiceName}/
├── {Host}.Bootstrapper/
├── {Host}.Api/
├── {Host}.Scheduler/               # optional
├── {Host}.BackgroundServices/      # optional
├── {Gateway}.Gateway/              # optional
├── {Host}.UI/                      # optional
├── Functions/{App}.FunctionApp/    # optional
├── Aspire/
│   ├── AppHost/
│   └── ServiceDefaults/
├── Test/
│   ├── Test.Unit/
│   ├── Test.Integration/
│   ├── Test.Architecture/
│   ├── Test.PlaywrightUI/
│   ├── Test.Load/
│   ├── Test.Benchmarks/
│   └── Test.Support/
├── Directory.Packages.props
├── global.json
├── nuget.config
└── {SolutionName}.slnx
```

Reference implementation: `sampleapp/src/`.

Note: Domain rules and specifications live in `Domain.Model/Rules/` (or `Domain.Model/Specifications/`). A separate `Domain.Rules` project is not required.

---

## Dependency Direction (Contract)

Required flow:

```
Domain.Shared <- Domain.Model
            \-> Application.Models <- Application.Contracts <- Application.Services
                                                \-> Application.MessageHandlers
Domain.Model -> Infrastructure.Data -> Infrastructure.Repositories
Application.Contracts -> Infrastructure.Repositories
Application + Infrastructure -> {Host}.Bootstrapper
{Host}.Bootstrapper -> host projects (API/Scheduler/FunctionApp)
```

### Host Rules

- API/Scheduler/FunctionApp reference Bootstrapper and add host-only config.
- Gateway and UI follow their own skills but must not violate core dependency direction.
- Optional hosts should be removable without breaking core layer compilation.

---

## `.slnx` Requirement

Use XML-based `.slnx` as the final solution artifact.

- Preferred: author `.slnx` directly from the reference pattern.
- If CLI scaffolding creates `.sln`, migrate and remove the `.sln` before continuing.

Do not keep both formats in active use.

---

## SDK and Package Management

### `global.json`

- Pin to latest stable installed SDK.
- Keep `rollForward` as `latestFeature`.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

### `Directory.Packages.props`

- `ManagePackageVersionsCentrally=true`.
- No package versions in project-level `<PackageReference>` entries.
- Update package versions centrally only.

### `.csproj` Conventions

- Target the latest stable TFM supported by the pinned SDK.
- Enable `ImplicitUsings` and `Nullable`.
- API/Gateway use `Microsoft.NET.Sdk.Web`; library projects use `Microsoft.NET.Sdk`.

---

## Infrastructure Naming Rules

- One infrastructure project per external integration.
- Use service-descriptive suffixes (for example: `Infrastructure.Notification`, `Infrastructure.EntraExt`).
- Expose contracts in Application layer; keep implementations in Infrastructure.
- Register each infrastructure module through Bootstrapper extension methods.

---

## Minimal Reference Matrix

| Project | Direct References (minimum) |
|---|---|
| `Domain.Shared` | none |
| `Domain.Model` | `Domain.Shared` |
| `Application.Models` | shared/common abstractions as needed |
| `Application.Contracts` | `Application.Models`, `Domain.Model`, `Domain.Shared` |
| `Application.Services` | `Application.Contracts`, `Application.Models`, domain projects |
| `Infrastructure.Data` | domain projects |
| `Infrastructure.Repositories` | `Application.Contracts`, `Infrastructure.Data` |
| `{Host}.Bootstrapper` | app/infrastructure implementations |
| `{Host}.Api` / `{Host}.Scheduler` / `FunctionApp` | `{Host}.Bootstrapper` (+ host-specific packages) |

Adjust optional dependencies per enabled features without inverting layer direction.

---

## Verification

- [ ] `.slnx` exists and is the active solution format
- [ ] `dotnet build` succeeds from `src/`
- [ ] `Directory.Packages.props` is present and controls package versions
- [ ] `global.json` uses `latestFeature` roll-forward
- [ ] `nuget.config` includes required public/private feeds
- [ ] Domain projects do not reference Application/Infrastructure
- [ ] Host projects depend on Bootstrapper instead of duplicating shared DI wiring
- [ ] Optional hosts can be removed without breaking core layer compilation
- [ ] token placeholders follow [placeholder-tokens.md](../placeholder-tokens.md)