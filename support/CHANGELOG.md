# Changelog

This file tracks the current instruction-set release only. Historical entries are intentionally discarded.

## [1.0] — 2026-04-01 — Initial baseline release

### Included at baseline

- 4-phase workflow: Domain Discovery → Resource Definition → Implementation Planning → Implementation (4a–4g)
- Manifest-driven token-aware phase loading (`_manifest.json`, `phase-load-packs.json`)
- `modeExclusions` in `_manifest.json` as the source of truth for `full`, `lite`, and `api-only` modes
- Dependency-aware `get-phase-load-set.ps1` with transitive `requires`/`dependencies` resolution, topological ordering, and budget reporting
- `scripts/generate-phase-load-packs.ps1` as the only driver of phase/mode pack generation
- `skills/identity-management.md` placed at canonical `phase-4f`
- `ai/SKILL.md` scoped to Phase 4 execution guidance; prompt starters in `support/prompt-patterns.md`
- TaskFlow sample app as read-only reference implementation
- `support/HANDOFF.md` for session state preservation across context boundaries
- Lint checks for manifest load-orchestration invariants
- CI workflow (`instruction-preflight.yml`) validating manifest, load packs, lint, and YAML schema on every push/PR
