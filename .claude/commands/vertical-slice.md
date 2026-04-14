# Add Vertical Slice

Add a new entity vertical slice to an existing C#/.NET solution.

**Entity and target:** $ARGUMENTS

All instruction files live under `.instructions/` in the project root. All file references below are relative to that folder.

## Instructions

You are adding a complete entity slice (domain → data → application → API → tests) to an existing solution scaffolded with this instruction set.

1. Read `.instructions/support/vertical-slice-checklist.md` — follow the fast-path section.
2. Read `.instructions/ai/placeholder-tokens.md` for naming conventions.
3. Load the templates listed in the checklist's "Load Set for Slice" section (under `.instructions/templates/`).
4. If `resource-implementation.yaml` exists in the project root, read it for `scaffoldMode` and `testingProfile`.

## Pre-Flight

- Verify `dotnet build` passes on the existing solution.
- Locate `RegisterServices.cs`, both DbContext files, and `WebApplicationBuilderExtensions.cs`.
- Review existing entity patterns in the target project for consistency.

## Execution Order

1. Entity + enums in `Domain.Model`
2. EF configuration in `Infrastructure.Data`
3. `DbSet<Entity>` in both DbContexts
4. Repository interface + implementations (Trxn + Query)
5. DTO + SearchFilter in `Application.Models`
6. Mapper in `Application.Mappers`
7. StructureValidator in `Application.Services/Rules`
8. Service + interface
9. Endpoint
10. Wire DI in `RegisterServices.cs`
11. Map endpoints in `WebApplicationBuilderExtensions.cs`
12. Migration: `dotnet ef migrations add Add{Entity} ...`

## Rules

- Generate code in `src/`, `tests/`, and project root. Never modify files under `.instructions/`.
- Do not modify shared infrastructure — only add entity-specific files.
- Do not skip DI registration or endpoint mapping.
- Follow existing code patterns for consistency.

## Gate

```bash
dotnet build
dotnet test --filter "TestCategory={Entity}"
```

Report files created, wiring steps completed, and gate results.
