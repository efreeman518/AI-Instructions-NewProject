# Shared Understanding Interview (Phase 1)

Use this file during Phase 1 before writing `.scaffold/domain-specification.yaml`.

Goal: interview the developer until the AI and developer share the same domain model, vocabulary, and decision context. Do not rush to YAML. The YAML is only valid after the interview branches below are confirmed, defaulted, or explicitly deferred.

> **First time?** [`../support/phase-1-worked-example.md`](../support/phase-1-worked-example.md) shows a condensed transcript of the actual interview that produced the TaskFlow reference app - pacing, branch recaps, and how the AI handles a mid-interview correction.

## Output Artifacts

Phase 1 produces all three files under `.scaffold/` in the target project (create the directory at project root if absent):

1. `.scaffold/domain-specification.yaml`
2. `.scaffold/UBIQUITOUS-LANGUAGE.md`
3. `.scaffold/DESIGN-DECISIONS.md`

Use [../templates/ubiquitous-language-template.md](../templates/ubiquitous-language-template.md) and [../templates/design-decisions-template.md](../templates/design-decisions-template.md).

## Interview Rules

- Ask questions in small batches. Prefer 3-7 related questions per branch.
- After each branch, summarize the current understanding and ask the developer to correct it.
- Track every non-obvious choice in `.scaffold/DESIGN-DECISIONS.md`.
- Track every accepted domain term, rejected synonym, state, event, action, role, and policy term in `.scaffold/UBIQUITOUS-LANGUAGE.md`.
- Resolve dependent decisions in order. Do not finalize a child decision when its parent decision is open.
- Use canonical defaults only where the instruction set defines them. State the default and record it.
- If a decision is not needed for current scaffold correctness, mark it `deferred` with the phase that must revisit it.

## Branch Order

Walk these branches in order. Revisit earlier branches when a later answer changes them.

| Branch | Resolve | Key Dependencies |
|---|---|---|
| Purpose | business problem, success criteria, primary users | none |
| Actors and roles | human roles, system actors, permissions vocabulary | purpose |
| Ubiquitous language | accepted terms, rejected synonyms, naming conflicts | purpose, actors |
| Entities and aggregates | entities, ownership, aggregate roots, tenant scope | language |
| Relationships | ownership, reference, self-reference, many-to-many, polymorphic ownership | entities |
| Lifecycle | states, transitions, commands/actions, terminal states | entities, relationships |
| Rules and policies | invariants, policy matrices, quotas, conflict handling | lifecycle |
| Events and workflows | business events, async reactions, scheduled work, compensation | lifecycle, rules |
| Data and resources | store fit, external dependencies, local emulator/no-op posture | entities, workflows |
| Security and compliance | tenancy, auth scenario, audit, retention, sensitive data | actors, entities, resources |
| Interfaces | API, UI, background hosts, integrations, AI capabilities | actors, workflows, resources |
| Delivery constraints | scaffold mode, test profile, regions, cost, team constraints | all prior branches |

> **Heads-up - Phase 2 will open with packaging strategy.** The very first Phase 2 question asks whether the project has a private NuGet feed for shared base contracts (e.g., `EF.*`) or whether the scaffold should generate equivalent packable projects under `src/Packages/<Prefix>.*`. Flag any constraints here (corporate feed policy, prefix conventions) so Phase 2 doesn't re-discover them. Full details: [resource-implementation-schema.md section Discovery Conversation Pattern](resource-implementation-schema.md#discovery-conversation-pattern).

## Branch Recap Format

After each branch, use this exact structure:

```markdown
### Branch: {name}

Current understanding:
- ...

Confirmed language:
- `{Term}` means ...

Decisions:
- `D-###` {decision} -> {selected option}; depends on: {D-### or none}

Open conflicts:
- None

Deferred:
- None
```

Ask: `Is this branch correct, or should anything change before I continue?`

## Decision Dependency Rules

Parent decisions must close before child decisions:

- Tenant model -> auth scenario -> tenant filters -> resource partitioning -> endpoint route shape.
- Entity ownership -> relationship type -> storage mapping -> repository order -> vertical slice order.
- Lifecycle states -> commands/actions -> events -> messaging/scheduler -> notification/AI hooks.
- Compliance classification -> audit/retention/encryption -> data store -> tests/IaC.
- UI/client needs -> API shape -> DTO/search filters -> endpoint tests.
- External dependency mode -> local boot behavior -> no-op stubs/emulators -> final scaffold gate.

If a child branch exposes a parent conflict, pause and reopen the parent branch.

## Shared Understanding Gate

Before writing Phase 1 outputs, confirm:

- [ ] Each branch is `confirmed`, `defaulted`, or `deferred`.
- [ ] Every entity, state, event, command/action, role, policy, and value object has a language entry.
- [ ] Every rejected synonym or ambiguous term is recorded.
- [ ] Every non-obvious design choice has a decision record with dependencies.
- [ ] No open decision blocks Phase 2 resource mapping.
- [ ] Developer has reviewed the final recap.

Only then write `.scaffold/domain-specification.yaml`, `.scaffold/UBIQUITOUS-LANGUAGE.md`, and `.scaffold/DESIGN-DECISIONS.md`.

## Application Style Decision

Capture this up front in Phase 1: `applicationStyle: service | cqrs | switch` (default `service`). Explain whether HTTP endpoints should inject `I{Entity}Service`, specific CQRS handlers, or both behind a runtime switch. If `cqrs` or `switch`, preserve DTO/routes unless the domain discovery proves a route change is required.
