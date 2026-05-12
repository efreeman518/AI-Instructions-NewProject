# Placeholder Token Glossary

When generating code from templates and skill files, substitute these placeholder tokens with the actual values from the user's domain inputs. This glossary is the canonical reference for all tokens.

## Token Definitions

| Token | Source | Notes |
|-------|--------|-------|
| `{Project}` | `ProjectName` | Primary project and namespace prefix. |
| `{ProjectName}` | `ProjectName` | Markdown/document templates that should display the full project name. Prefer `{Project}` for code templates. |
| `{Org}` | `OrganizationName` | Optional org prefix. If present, full namespace becomes `{Org}.{Project}`. |
| `{App}` | Derived from `{Project}` | Application namespace prefix. Used in `{App}DbContextTrxn` and `{App}DbContextQuery`. |
| `{Host}` | Derived from `{Project}` or `{Org}.{Project}` | Host project prefix. Used in `{Host}.Api`, `{Host}.Gateway`, `{Host}.Scheduler`. |
| `{Entity}` | Entity `name` | Entity class, file, and method name. |
| `{entity}` | Entity `name` with lower first character | Local variables, parameters, route values. |
| `{Entities}` | Pluralized entity name | Display and feature grouping name. |
| `{entities}` | Lower-cased pluralized entity name | URL path or collection variable. |
| `{entity-route}` | Kebab-cased entity name | URL-safe route segment. |
| `{ChildEntity}` | Child entity `name` | Child entity class name. |
| `{childEntity}` | Child entity `name` with lower first character | Child variables and parameters. |
| `{ChildEntity}s` | Pluralized child entity name | Default child collection property. |
| `{Children}` | Child collection name | Use when the collection name differs from `{ChildEntity}s`. |
| `{Feature}` | Defaults to `{Entities}` | Uno feature folder/service grouping. |
| `{Gateway}` | Same as `{Host}` | Compose gateway project name as `{Gateway}.Gateway`. |
| `{SolutionName}` | Derived from `{Org}.{Project}` or `{Project}` | `.slnx` file name and solution prefix. |
| `{entra-tenant-id}` | `authProvider` config | Azure Entra tenant GUID. |
| `{api-client-id}` | `authProvider` config | API app registration client ID. |
| `{Agent}` | Agent `name` | Agent class/service prefix. |
| `{agent-route}` | Kebab-cased agent name | Agent endpoint route segment. |
| `{Tool}` | Tool or function name | AI function tool class name. |
| `{SearchIndex}` | Search config index name | Azure AI Search index name. |
| `{ValueObject}` | Phase 1 language artifact | Value object term accepted in `.scaffold/UBIQUITOUS-LANGUAGE.md`. |
| `{Role}` | Phase 1 language artifact | Actor or authorization role term accepted in `.scaffold/UBIQUITOUS-LANGUAGE.md`. |
| `{State}` | Phase 1 language artifact | Lifecycle state term accepted in `.scaffold/UBIQUITOUS-LANGUAGE.md`. |
| `{PolicyName}` | Phase 1 language artifact | Domain policy/rule term accepted in `.scaffold/UBIQUITOUS-LANGUAGE.md`. |
| `{ExternalSystem}` | Phase 1 language artifact | External system term accepted in `.scaffold/UBIQUITOUS-LANGUAGE.md`. |

## Casing Conventions

> **Naming conflicts:** Avoid entity names that collide with C# framework types. Canonical list and safe alternatives: [domain-specification-schema.md — Entities section](domain-specification-schema.md#entities).

| Convention | Rule | Example |
|------------|------|---------|
| **PascalCase** | First letter of each word capitalized, no separators | `TodoItem`, `TeamMember` |
| **camelCase** | First letter lowercase, subsequent words capitalized | `todoItem`, `teamMember` |
| **kebab-case** | All lowercase, words separated by hyphens | `todo-item` |
| **UPPER_SNAKE** | All uppercase, words separated by underscores | Used only in environment variables, not in tokens |

## Derivation Rules

1. **`{App}` = `{Project}`** — always identical. Use `{App}` when the context is the application namespace (e.g., `{App}DbContextTrxn`). Use `{Project}` when the context is the project/solution name.
2. **`{Host}`** — if `OrganizationName` is provided: `{Org}.{Project}`. Otherwise: `{Project}`.
3. **`{Gateway}`** — same as `{Host}`. Compose the gateway project name as `{Gateway}.Gateway`.
4. **`{Feature}`** — defaults to `{Entities}` (plural entity name) unless explicitly provided. Groups UI services and models into feature folders (e.g., `Services/TodoItems/`).
5. **Pluralization** — use standard English pluralization rules. `TodoItem` → `TodoItems`, `Category` → `Categories`, `Reminder` → `Reminders`.
6. **Route segments** — for URL paths, use the lowercase/kebab-case form. Multi-word entities: `TodoItem` → `todo-item`, `TeamMember` → `team-member`.
7. **Aspire project references** — In `AppHost/AppHost.cs`, `builder.AddProject<Projects.X>()` uses the C# identifier form of the `.csproj` path, where dots and hyphens become underscores. For example: project `TaskFlow.Api` → `Projects.TaskFlow_Api`. This is automatic — just be aware when reading or writing AppHost code.

---

## File Naming Conventions

Canonical file name patterns for generated artifacts. Use these consistently across all generated code.

| Artifact | Pattern |
|---|---|
| Entity | `{Entity}.cs` |
| EF config | `{Entity}Configuration.cs` |
| Write repo | `{Entity}RepositoryTrxn.cs` |
| Read repo | `{Entity}RepositoryQuery.cs` |
| Repo interface | `I{Entity}RepositoryTrxn.cs` / `I{Entity}RepositoryQuery.cs` |
| Updater | `{Entity}Updater.cs` |
| DTO | `{Entity}Dto.cs` |
| Search filter | `{Entity}SearchFilter.cs` |
| Mapper | `{Entity}Mapper.cs` |
| Service | `{Entity}Service.cs` |
| Service interface | `I{Entity}Service.cs` |
| Endpoint | `{Entity}Endpoints.cs` |
| Message handler | `{Event}Handler.cs` |
| Health check | `{Target}HealthCheck.cs` |
| Settings POCO | `{Entity}ServiceSettings.cs` |
| Structure validator | `{Entity}StructureValidator.cs` |
| Domain rules | `Rules/{RuleName}Rule.cs` |
| Dockerfile | `Dockerfile` |

---

## Canonical Event And Publisher Naming

Use these defaults when scaffolding event-driven flows:

1. Cross-process event payloads are integration contracts.
2. Place transport payload records in `Application.Contracts.Events`.
3. Use `IIntegrationEventPublisher` as the publish abstraction for external buses.
4. Name publisher implementations by transport, for example `ServiceBusIntegrationEventPublisher` and `NoOpIntegrationEventPublisher`.
5. Reserve `Domain.*` events for aggregate-local invariants and in-process domain dispatch.
6. Do not name external publisher abstractions as `IDomainEventPublisher`.

Default naming patterns:

| Artifact | Pattern |
|---|---|
| Integration event contract | `{Entity}{Action}Event` (in `Application.Contracts.Events`) |
| Integration publisher interface | `IIntegrationEventPublisher` |
| Service Bus publisher | `ServiceBusIntegrationEventPublisher` |
| No-op publisher | `NoOpIntegrationEventPublisher` |
