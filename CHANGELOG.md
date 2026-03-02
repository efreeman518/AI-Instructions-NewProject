# Changelog

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
