# Template Index

Quick lookup: "I need to scaffold X" → load these files.

## Phase 1 Discovery Artifacts

| Artifact | Template / Instruction |
|---|---|
| Shared understanding interview | `ai/shared-understanding-interview.md` |
| Domain vocabulary | `ubiquitous-language-template.md` |
| Decision dependency log | `design-decisions-template.md` |

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
| Architecture / E2E / Load / Benchmarks / Playwright | `test-templates-quality.md` | 5d | `skills/testing-quality.md` |
| Complete reference (all tests) | `test-templates.md` | on-demand | `skills/testing.md` |

Testing skills (two files only):

- `skills/testing.md` — Phase 5a/5b/5c TDD, harness tiers, integration hosts, template map.
- `skills/testing-quality.md` — Phase 5d quality gates and hosted Playwright UI.

## Contracts + TDD

| Artifact | Instruction File | Phase |
|---|---|---|
| Contract scaffolding (interfaces, DTOs, shells) | `ai/contract-scaffolding.md` | 4 |
| TDD red/green protocol | `ai/tdd-protocol.md` | 5a/5b |

## UI (choose one)

Scaffold either Uno Platform (multi-target WASM + mobile + desktop) or Blazor (Server or WebAssembly). Both call the Gateway; both can ship side-by-side under `src/UI/` when needed. See each skill for selection guidance.

### Uno Platform

> **Design Standard:** Each main entity gets two pages — `{Entity}ListPage` (list/search) and `{Entity}Page` (unified add/edit + children). No separate detail or create pages. See templates for the pattern.

| Artifact | Template | Required Skill |
|---|---|---|
| Models + Services | `uno-ui-client-layer.md` | `skills/ui-uno.md` |
| MVUX model (List + Page) | `uno-mvux-model-template.md` | `skills/ui-uno.md` |
| XAML page (List + Entity) | `uno-xaml-page-template.md` | `skills/ui-uno.md` |

### Blazor

> **Design Standard:** MudBlazor shell + pages in `Components/Pages/`. Scoped `FloatService` for cross-page state and progress (no cascading parameters). Refit client interface for gateway calls. See `skills/ui-blazor.md` for the full pattern.

| Artifact | Instruction File |
|---|---|
| Complete pattern (Program.cs, FloatService, Refit interface, MainLayout, pages) | `skills/ui-blazor.md` |

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
| **5b — App Core + Runtime (TDD for app/API, tests-after for runtime)** | `data-mapping-template`, `service-template`, `endpoint-template`, `structure-validator-template`, `exception-handler-template`, `message-handler-template` (if events), `health-check-template`, **`test-templates-service`**, **`test-templates-endpoint`** |
| **5c — Optional Hosts** | `ui-client-layer`, `mvux-model-template`, `xaml-page-template` (Uno); host-specific templates per enabled host |
| **5d — Quality + Delivery** | **`test-templates-quality`**, `dockerfile-template` |
| **5e — Integration (Auth + AI)** | `ai-search-template`, `agent-template` (when AI in scope) |

> **Note:** Use the Phase Router in `START-AI.md` and the Phase 5 file table in `ai/SKILL.md` for authoritative per-phase file lists. This index is a human/AI quick-reference for "I need to scaffold X → load template Y".
