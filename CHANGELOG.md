# Changelog

## [1.3] — 2026-03-02
### Added
- `templates/dockerfile-template.md` — multi-stage chiseled Dockerfile pattern with variants
- OpenAPI/Scalar configuration section in `skills/api.md`
- Database seeding / reference data pattern in `skills/data-access.md`
- End-to-end error flow trace example in `skills/api.md`
- Test data builder pattern in `skills/testing.md`
- File naming conventions table in `placeholder-tokens.md`
- Prompt patterns section in `SKILL.md` for each phase
- Sample app file index in `sampleapp-patterns.md` with exact paths
- Context budget utilization guidance in `HANDOFF.md` (per-sub-phase budgets)
- `scripts/validate-domain-spec.ps1` — YAML schema validation for domain specs
- `scripts/validate-resource-impl.ps1` — YAML schema validation for resource implementations
- Template composition metadata (`generates`, `requires`) in `_manifest.json`
- Lint checks: skill file existence, manifest↔disk sync, undefined placeholder tokens (sections 7-9)
- Validation scripts wired into `preflight-instructions.ps1`

### Fixed
- `Entity.Create()` parameter order: `Create(tenantId, name)` across all test templates
- Mapper test no longer asserts audit fields (handled by `AuditInterceptor`)
- `AllRule<T>.ErrorMessage` — removed `default!` NPE risk, uses eager message collection
- All test template stubs filled with complete implementations
- E2E page object methods (`ItemExistsInGridAsync`, `ItemNotInGridAsync`) completed
- Quality test `BaseTest` assembly references fixed (`Domain.Model.{Entity}`)
- Integration test `CRUD_Pass` and `GetPage_ReturnsOk` fully implemented
- Structure validator template: added contextual token documentation
- Lint script section 6 updated for consolidated troubleshooting file

### Changed
- Merged `test-gotchas.md` into `troubleshooting.md` (single file, updated all references)
- Merged `skills/error-handling.md` inline into `skills/api.md` (error pipeline + mapping table + anti-patterns)
- Moved file naming conventions from `quick-reference.md` to `placeholder-tokens.md`
- Removed manual phase loading fallback lists from `SKILL.md` (use scripts only)
- Removed lite mode entity count guidance from `SKILL.md`
- `_manifest.json` updated: removed deleted files, added new files, refreshed token estimates

### Removed
- `test-gotchas.md` (merged into `troubleshooting.md`)
- `skills/error-handling.md` (merged into `skills/api.md`)

## [1.2] — 2026-03-XX
### Added
- `skills/observability.md` — structured logging, tracing, metrics, health checks
- `skills/error-handling.md` — cross-cutting error pipeline reference
- `skills/security.md` — hardening checklist (rate limiting, headers, CORS, scanning)
- `skills/migrations.md` — EF migration strategy, zero-downtime patterns
- `templates/structure-validator-template.md` — DTO structure validation
- `templates/exception-handler-template.md` — global exception-to-ProblemDetails handler
- `scripts/update-manifest.ps1` — automated manifest token estimation
- Schema validation gates (Phase 1→2, 2→3, 3→4 transition checklists)
- Skill dependency graph in `_manifest.json`
- `api-only` scaffolding mode
- Vertical slice fast-path for adding entities to existing projects
- Configurable context budget in `_manifest.json`
- Scaffolding telemetry format in `HANDOFF.md`
- Lite mode reference documentation in `SKILL.md`
- Git checkpoint protocol in `SKILL.md`

### Fixed
- XAML template `</uer:FeedView>` → `</utu:FeedView>`
- `AllRule<T>.ErrorMessage` NPE risk documented

### Changed
- Test templates now include one fully-implemented test method each
- `_manifest.json` schema extended with `dependencies` and `contextBudget`

## [1.1] — 2026-02-25
- Initial versioned release after PlanTrack trial run
