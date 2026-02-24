# Placeholder Token Glossary

When generating code from templates and skill files, substitute these placeholder tokens with the actual values from the user's domain inputs. This glossary is the canonical reference for all tokens.

## Token Definitions

| Token | Casing | Source | Example | Description |
|-------|--------|--------|---------|-------------|
| `{Project}` | PascalCase | `ProjectName` from domain inputs | `TaskFlow` | Primary project/namespace prefix. Used in namespaces, project names, and file paths. |
| `{Org}` | PascalCase | `OrganizationName` from domain inputs | `Contoso` | Organization prefix. If provided, full namespace is `{Org}.{Project}`. If omitted, use `{Project}` alone. |
| `{App}` | PascalCase | Derived — same as `{Project}` | `TaskFlow` | Application-level namespace prefix. Equivalent to `{Project}`. Used in `{App}DbContextTrxn`, `{App}DbContextQuery`, test assembly references. |
| `{Host}` | PascalCase | Derived — `{Project}` or `{Org}.{Project}` | `TaskFlow` | Full host project name prefix. Used in API/Gateway/Scheduler project names: `{Host}.Api`. |
| `{Entity}` | PascalCase | Entity `name` from domain inputs | `TodoItem` | Entity class name. Used in class names, file names, method names. |
| `{entity}` | camelCase | Entity `name` (lowered first char) | `todoItem` | Entity in camelCase. Used in route segments, parameter names, local variables. |
| `{Entities}` | PascalCase plural | Entity name pluralized | `TodoItems` | Plural display name. Used in endpoint summaries, page titles. |
| `{entities}` | camelCase plural | Entity name pluralized (lowered) | `todoItems` | Plural route segment. Used in URL paths. |
| `{entity-route}` | kebab-case | Entity name kebab-cased | `todo-item` | URL-safe route segment for multi-word entities. Use only in URL paths. |
| `{ChildEntity}` | PascalCase | Child entity `name` from domain inputs | `Comment` | Child entity class name in parent-child relationships. |
| `{childEntity}` | camelCase | Child entity name (lowered first char) | `comment` | Child entity in camelCase for parameters and variables. |
| `{ChildEntity}s` | PascalCase + s | Child entity name pluralized | `Comments` | Child collection property name on parent entity. |
| `{Children}` | PascalCase plural | Child collection property name | `Comments` | Short-form collection name when different from `{ChildEntity}s`. Matches the `children.name` field in domain inputs. |
| `{Feature}` | PascalCase | Feature area — typically same as `{Entity}` | `TodoItem` | UI service namespace grouping. Maps to feature folders in the Uno UI project. |
| `{Gateway}` | PascalCase | Derived — same as `{Host}` | `TaskFlow` | Gateway host prefix. Use `{Gateway}.Gateway` for the gateway project name/path. |
| `{SolutionName}` | PascalCase | Derived — `{Org}.{Project}` or `{Project}` | `TaskFlow` | Solution file name (`.slnx`). Matches the full qualified project prefix. |
| `{entra-tenant-id}` | GUID | From domain inputs `authProvider` config | `00000000-...` | Azure Entra ID tenant GUID. |
| `{api-client-id}` | GUID | From domain inputs `authProvider` config | `11111111-...` | API app registration client ID. |

## Casing Conventions

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
4. **`{Feature}`** — defaults to `{Entity}` unless explicitly provided. Groups UI services and models.
5. **Pluralization** — use standard English pluralization rules. `TodoItem` → `TodoItems`, `Category` → `Categories`, `Reminder` → `Reminders`.
6. **Route segments** — for URL paths, use the lowercase/kebab-case form. Multi-word entities: `TodoItem` → `todo-item`, `TeamMember` → `team-member`.
7. **Aspire project references** — In `AppHost/AppHost.cs`, `builder.AddProject<Projects.X>()` uses the C# identifier form of the `.csproj` path, where dots and hyphens become underscores. For example: project `TaskFlow.Api` → `Projects.TaskFlow_Api`. This is automatic — just be aware when reading or writing AppHost code.

## Usage Examples

Given domain inputs:
```yaml
ProjectName: TaskFlow
entities:
  - name: TodoItem
    children:
      - name: Comments
        entity: Comment
```

| Token | Resolved Value |
|-------|---------------|
| `{Project}` | `TaskFlow` |
| `{App}` | `TaskFlow` |
| `{Host}` | `TaskFlow` |
| `{Entity}` | `TodoItem` |
| `{entity}` | `todoItem` |
| `{Entities}` | `TodoItems` |
| `{entity-route}` | `todo-item` |
| `{ChildEntity}` | `Comment` |
| `{Children}` | `Comments` |
| `{Feature}` | `TodoItem` |
| `{Gateway}` | `TaskFlow` |
| `{SolutionName}` | `TaskFlow` |

## Sampleapp Token Mapping

The reference implementation (`sampleapp/`) demonstrates all tokens with these resolved values:

```yaml
Org: Sample
Project: TaskFlow
```

| Token | Resolved | Example File / Path |
|-------|----------|---------------------|
| `{Org}` | `Sample` | Prefix on library projects |
| `{Project}` | `TaskFlow` | `TaskFlow.Domain.Model/`, `TaskFlow.Api/` |
| `{App}` | `TaskFlow` | `TaskFlowDbContextTrxn`, `TaskFlowDbContextQuery` |
| `{Host}` | `TaskFlow` | `TaskFlow.Api/`, `TaskFlow.Bootstrapper/`, `TaskFlow.UI/` |
| `{SolutionName}` | `TaskFlow` | `TaskFlow.slnx` |
| `{Entity}` | `TodoItem` | `TodoItem.cs`, `TodoItemService.cs`, `TodoItemEndpoints.cs` |
| `{entity}` | `todoItem` | Route parameters, local variables |
| `{Entities}` | `TodoItems` | Endpoint group names, page titles |
| `{entities}` | `todoItems` | URL path segments: `/todoItems/search` |
| `{ChildEntity}` | `Comment` | `Comment.cs` (child of TodoItem) |
| `{Children}` | `Comments` | `TodoItem.Comments` collection property |
| `{Feature}` | `TodoItems` | `UI/Business/Services/TodoItems/` |
| `{Gateway}` | `TaskFlow` | `TaskFlow.Gateway/` project (`{Gateway}.Gateway`) |

> **Naming note:** Library projects use `{Org}.{Project}` prefix (e.g., `TaskFlow.Domain.Model`). Host/deployable projects use just `{Project}` prefix (e.g., `TaskFlow.Api`). This keeps host project names shorter while library names stay fully qualified.
