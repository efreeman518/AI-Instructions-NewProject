# Composition Wiring Patterns — Index

Cross-cutting patterns that span multiple files/projects, split by concern for phase-aligned loading.

For template-to-artifact lookups, see [../templates/index.md](../templates/index.md).
For shared library types (DbContextBase, DomainResult, IRequestContext, etc.), see [ef-packages-reference.md](ef-packages-reference.md).

---

## Pattern Files

Load only the pattern file relevant to the current phase. Do not preload all.

| File | Contains | Load Before |
|---|---|---|
| [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md) | DB context pooling, OnModelCreating order, startup tasks, seed data, scaffold migration strategy | Phase 5a, 5b |
| [../patterns/api-host-wiring.md](../patterns/api-host-wiring.md) | API startup sequence, request context resolution, conditional auth | Phase 5b, 5c |
| [../patterns/infrastructure-wiring.md](../patterns/infrastructure-wiring.md) | Multi-cache config, Aspire resource wiring | Phase 5c, 5d |
| [../patterns/expected-output-index.md](../patterns/expected-output-index.md) | Expected file layout when scaffolding is complete | On-demand (verification) |

---

## Pattern Selection Rules

1. Prefer template-owned implementation details over pattern duplication.
2. Use pattern files for orchestration decisions across projects, not per-file boilerplate.
3. If a pattern touches 3+ projects, treat it as a composite and verify all references.
4. When uncertain, default to the simplest pattern that preserves clean architecture boundaries.

---

## Verification Checklist

- [ ] Selected patterns map to enabled workloads only
- [ ] Each chosen pattern has an explicit primary reference loaded
- [ ] No duplicate implementation guidance copied from templates
- [ ] Cross-project wiring (AppHost/Gateway/Scheduler/UI) is internally consistent
