# Changelog

This file tracks the current instruction-set release only. Historical entries are intentionally discarded.

## [1.4] — 2026-03-07
### Added
- Manifest-driven `modeExclusions` in `_manifest.json` as the source of truth for `lite` and `api-only`
- Dependency-aware `get-phase-load-set.ps1` resolution with transitive `requires`/`dependencies`, topological ordering, and budget reporting
- Lint checks for manifest load-orchestration invariants (`modeExclusions`, auth phase placement)

### Fixed
- `phase-load-packs.json` generation no longer parses prose from `SKILL.md` to infer mode exclusions
- `skills/identity-management.md` moved to canonical `phase-4f` to match auth finalization guidance
- Canonical Uno gate now requires actual target build validation in addition to `uno-check`

### Changed
- `scripts/generate-phase-load-packs.ps1` now uses `_manifest.json` as the only source of truth for phase/mode pack generation
- `README.md`, `SKILL.md`, and `START-AI.md` updated to describe manifest-driven load resolution
- `SKILL.md` trimmed to Phase 4 execution guidance; prompt starters moved to `prompt-patterns.md`
