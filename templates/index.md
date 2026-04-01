# Template Index

Quick lookup: "I need to scaffold X" → load these files.

## Backend Vertical Slice (Entity End-to-End)

| Artifact | Template | Required Skill |
|---|---|---|
| Entity class | `entity-template.md` | `skills/domain-model.md` |
| EF configuration | `ef-configuration-template.md` | `skills/data-persistence.md` |
| Repository (read/write) | `repository-template.md` | `skills/data-persistence.md` |
| Domain rules | `domain-rules-template.md` | `skills/domain-model.md` |
| DTOs + Mappers | `data-mapping-template.md` | `skills/application-layer.md` |
| Service + interface | `service-template.md` | `skills/application-layer.md` |
| Endpoint | `endpoint-template.md` | `skills/api.md` |
| Structure validator | `structure-validator-template.md` | `skills/application-layer.md` |
| Exception handler | `exception-handler-template.md` | `skills/api.md` |
| Message handler | `message-handler-template.md` | `skills/application-layer.md` |
| Updater | `updater-template.md` | `skills/data-persistence.md` |
| appsettings | `appsettings-template.md` | `skills/configuration-secrets.md` |

## Tests

| Artifact | Template | Required Skill |
|---|---|---|
| Unit / Integration / E2E / Quality | `test-templates.md` | `skills/testing.md` |

## UI (Uno Platform)

| Artifact | Template | Required Skill |
|---|---|---|
| Models + Services | `ui-client-layer.md` | `skills/uno-ui.md` |
| MVUX model | `mvux-model-template.md` | `skills/uno-ui.md` |
| XAML page | `xaml-page-template.md` | `skills/uno-ui.md` |

## AI Integration

| Artifact | Template | Required Skill |
|---|---|---|
| Search service | `ai-search-template.md` | `skills/ai-integration.md` |
| Agent service | `agent-template.md` | `skills/ai-integration.md` |

## Infrastructure

| Artifact | Template | Required Skill |
|---|---|---|
| Dockerfile | `dockerfile-template.md` | `skills/solution-structure.md` + `skills/aspire.md` |

## Phase-to-Template Mapping

| Phase | Templates to Load |
|---|---|
| **4a — Foundation** | `entity-template`, `ef-configuration-template`, `repository-template`, `domain-rules-template`, `appsettings-template`, `updater-template` (if needed) |
| **4b — App Core** | `data-mapping-template`, `service-template`, `endpoint-template`, `structure-validator-template`, `exception-handler-template`, `message-handler-template` (if events) |
| **4d — UI** | `ui-client-layer`, `mvux-model-template`, `xaml-page-template` |
| **4e — Quality** | `test-templates`, `dockerfile-template` |
| **4g — AI** | `ai-search-template`, `agent-template` |

> **Note:** Use `phase-load-packs.json` for authoritative per-phase file lists. This index is a human/AI quick-reference.
