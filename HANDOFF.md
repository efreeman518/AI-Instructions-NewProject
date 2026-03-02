# HANDOFF.md

Create this file in the target project root during Phase 4 execution when AI context is getting high and the developer needs to start a new session. This provides explicit resume instructions — not a running log.

**When to create:** During implementation (Phase 4+), when context usage is high or a natural session boundary is reached. Not needed during Phases 1-3 (design artifacts serve as the handoff).

## Current Phase
- Phase: <4a-foundation | 4b-app-core | 4c-runtime-edge | 4d-optional-hosts | 4e-quality-delivery | 4f-auth | 4g-ai-integration>
- Scope in this session: <what was attempted>

## Completed
- <artifact or step>
- <artifact or step>

## Validation
- Command: `<exact command>`
- Result: <pass | fail>
- Notes: <1-2 lines>

## Blockers
### Blocker 1
- Classification: <code-generation | infrastructure>
- Symptom: <short error summary>
- Next action: <exact next step + owner>

## Session Metrics (Optional)
Track per sub-phase to identify which skills/templates need instruction improvements. Feed back into UPDATE-INSTRUCTIONS.md.

| Sub-Phase | First-Pass Build | Fix Passes | Blocking Skill | Notes |
|-----------|-----------------|------------|----------------|-------|
| 4a-foundation | pass | 0 | — | — |
| 4b-app-core | fail | 2 | service-template | SaveChangesAsync overload |

## Context Budget Utilization

Recommended token budget per sub-phase (within the 30K phase ceiling):

| Sub-Phase | Budget | Key Files |
|-----------|--------|-----------|
| 4a — Foundation | ~8K | SKILL.md + placeholder-tokens + 4 skills + 5 templates |
| 4b — App Core | ~7K | 3 skills + 4-6 templates |
| 4c — Runtime/Edge | ~6K | 2-4 skills (only enabled concerns) |
| 4d — Optional Hosts | ~5K | 1-2 skills + relevant templates |
| 4e — Quality | ~6K | testing skill + 1-2 test templates + iac/cicd |
| 4f — Auth | ~3K | identity-management only |
| 4g — AI Integration | ~5K | ai-integration + search/agent templates |

**Early warning:** If context exceeds ~25K tokens before completing a sub-phase, create/update HANDOFF.md and start a new session. Unload completed sub-phase docs before loading next sub-phase.

## Next Load Set (for next AI turn)
Load only these files:
- <file 1>
- <file 2>
- <file 3>

## Next Step
- <single highest-value next action>
