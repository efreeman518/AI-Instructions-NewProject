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

## Tests (Split by Phase)

| Artifact | Template | Phase | Required Skill |
|---|---|---|---|
| Domain entity + rule tests | `test-templates-domain.md` | 5a | `skills/testing.md` |
| Repository tests | `test-templates-repository.md` | 5a | `skills/testing.md` |
| Service + mapper tests | `test-templates-service.md` | 5b | `skills/testing.md` |
| Endpoint integration tests | `test-templates-endpoint.md` | 5b | `skills/testing.md` |
| Architecture / E2E / Load / Benchmarks | `test-templates-quality.md` | 5e | `skills/testing.md` |
| Complete reference (all tests) | `test-templates.md` | on-demand | `skills/testing.md` |

## Contracts + TDD

| Artifact | Instruction File | Phase |
|---|---|---|
| Contract scaffolding (interfaces, DTOs, shells) | `ai/contract-scaffolding.md` | 4 |
| TDD red/green protocol | `ai/tdd-protocol.md` | 5a/5b |

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
| Health checks | `health-check-template.md` | `skills/observability.md` |
| Dockerfile | `dockerfile-template.md` | `skills/solution-structure.md` + `skills/aspire.md` |

## Phase-to-Template Mapping

| Phase | Templates to Load |
|---|---|
| **4 — Contracts** | Solution structure + contracts (see `ai/contract-scaffolding.md`) |
| **5a — Foundation (TDD)** | `entity-template`, `ef-configuration-template`, `repository-template`, `domain-rules-template`, `appsettings-template`, `updater-template` (if needed), **`test-templates-domain`**, **`test-templates-repository`** |
| **5b — App Core (TDD)** | `data-mapping-template`, `service-template`, `endpoint-template`, `structure-validator-template`, `exception-handler-template`, `message-handler-template` (if events), **`test-templates-service`**, **`test-templates-endpoint`** |
| **5c — Runtime/Edge** | `health-check-template` |
| **5d — UI** | `ui-client-layer`, `mvux-model-template`, `xaml-page-template` |
| **5e — Quality Gates** | **`test-templates-quality`**, `dockerfile-template` |
| **5g — AI** | `ai-search-template`, `agent-template` |

> **Note:** Use `phase-load-packs.json` for authoritative per-phase file lists. This index is a human/AI quick-reference.
