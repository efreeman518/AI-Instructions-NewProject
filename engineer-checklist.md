# Engineer Execution Checklist

Use this file as the **single execution path** for compile/run verification while the AI scaffolds code. It is intentionally checklist-first and avoids deep troubleshooting playbooks.

---

## How to Use This Checklist

1. Run **Preflight** once per machine/repo.
2. After every AI phase, run the **Phase Execution Loop**.
3. Run only the optional sections that match enabled hosts/workloads.
4. If a step fails, create `HANDOFF.md` if missing, log the issue, keep scope moving, and return later.
5. Treat `sampleapp/` as code reference only; never build/compile/test inside `sampleapp/`.

---

## Scope Selection (Pick One)

- [ ] **API-only baseline**: Foundation + App Core + API verification
- [ ] **API + services**: API baseline + Gateway/Aspire/Scheduler as enabled
- [ ] **Full app**: API + services + Function App + Uno UI + IaC as enabled

---

## Preflight (Run Once)

### Development Tools
- [ ] Git repo initialized with `.gitignore` for .NET
- [ ] `.NET SDK` installed (`dotnet --version`)
- [ ] Docker running (if Aspire uses SQL/Redis containers)
- [ ] `nuget.config` includes `nuget.org` + all custom/private feeds
- [ ] EF tools installed (`dotnet tool install -g dotnet-ef`)
- [ ] Functions Core Tools installed (`func --version`) *(if using Functions)*
- [ ] Uno templates installed (`dotnet new install Uno.Templates`) *(if using Uno UI)*
- [ ] Uno.Check installed (`dotnet tool install -g uno.check`) *(if using Uno UI)*
- [ ] Kiota CLI installed (`dotnet tool install -g Microsoft.OpenApi.Kiota`) *(if using Uno UI)*

### AI Assistant — MCP Servers

Configure these in your AI client (VS Code `settings.json` or Claude Desktop config) so the AI can look up current docs and interact with tools during scaffolding.

- [ ] MCP servers configured per [GET-STARTED-human.md](GET-STARTED-human.md) (Essential + phase-relevant)

---

## Phase Execution Loop (Run After Each Scaffolding Phase)

Run from solution root:

```powershell
dotnet restore
dotnet build
dotnet test --filter "TestCategory=Unit"
```

- [ ] Restore succeeds
- [ ] Build succeeds
- [ ] Unit tests pass
- [ ] Results recorded in `HANDOFF.md` (create if missing) if any step fails

If build/test fails after one AI code-fix pass, keep moving with non-blocked phases and return later.

---

## Foundation Exit Criteria

(After: `solution-structure` → `domain-model` → `data-access`)

- [ ] Project structure matches `skills/solution-structure.md`
- [ ] Domain entities exist in expected folders/namespaces
- [ ] DbContext files compile
- [ ] Initial migration command is ready:

```powershell
dotnet ef migrations add InitialCreate `
  --project src/Infrastructure/{Project}.Infrastructure.Data `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn
```

---

## App Core Exit Criteria

(After: `application-layer` → `bootstrapper` → `api`)

- [ ] Service + repository registrations added in `RegisterServices.cs`
- [ ] API endpoint mappings added in `WebApplicationBuilderExtensions.cs`
- [ ] `DbSet<{Entity}>` exists in Trxn + Query DbContexts
- [ ] API host builds cleanly

---

## Runtime/Host Checks (Enabled Features Only)

### Aspire AppHost

- [ ] `src/Aspire/AppHost/AppHost.csproj` has `<PackageReference Include="Aspire.Hosting.AppHost" />`
- [ ] Required Aspire CLI env vars are set before terminal `dotnet run`
- [ ] `dotnet build src/Aspire/AppHost` succeeds
- [ ] `dotnet run --project src/Aspire/AppHost` starts resources

### Gateway

- [ ] Gateway build succeeds
- [ ] Gateway can route to API via configured cluster/service discovery

### Scheduler (TickerQ)

- [ ] Scheduler connection string configured
- [ ] Scheduler host starts once (`dotnet run --project src/{Host}/{Host}.Scheduler`)
- [ ] Deployment script generated/applied if required

### Function App

- [ ] `local.settings.json` contains required runtime keys and trigger bindings
- [ ] Azurite/dev-tunnel/ngrok started if required by triggers
- [ ] `func host start --verbose` runs without immediate startup failure

### Uno UI

- [ ] `uno-check` validates workloads
- [ ] Gateway/OpenAPI endpoint reachable for client generation
- [ ] Kiota client generation completes (if used)
- [ ] UI runs on selected target (`net10.0-browserwasm` or `net10.0-desktop`)

---

## Delivery Checks

- [ ] Endpoint and integration tests pass for current scope
- [ ] Optional architecture tests pass (if included)
- [ ] `az bicep build --file infra/main.bicep` succeeds *(if IaC enabled)*
- [ ] Aspire ↔ IaC names/connection strings are aligned

---

## Failure Session State Template (`HANDOFF.md`)

When a step fails, create `HANDOFF.md` if needed, then add this:

```markdown
### Execution Blocker
- Step: <checklist step>
- Command: <command run>
- Symptom: <short error summary>
- Classification: code-generation | infrastructure
- Next action: <what engineer will do next>
```

Example:

```markdown
### Execution Blocker
- Step: Phase Execution Loop -> `dotnet restore`
- Command: dotnet restore src/TaskFlow.slnx
- Symptom: NU1301 Unable to load the service index for source https://pkgs.dev.azure.com/<org>/<feed>/nuget/v3/index.json
- Classification: infrastructure
- Next action: Engineer validates feed credentials and `nuget.config`, then reruns restore before AI continues phase scaffolding
```

---

## Quick Commands

```powershell
# Core loop
dotnet restore
dotnet build
dotnet test --filter "TestCategory=Unit"

# Broader validation
dotnet test
dotnet run --project src/Aspire/AppHost
az bicep build --file infra/main.bicep
```
