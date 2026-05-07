# Operational Protocols

Single source of truth for the protocols an AI session needs at the boundaries of normal work — when something fails, when state changes, when an assumption breaks, when context limits matter.

The Phase 5 **Decision Table** at the top of [../ai/SKILL.md](../ai/SKILL.md) is the fast-lookup index. This file is the detail.

## Fail-Fast Protocol

After every build:

- **Code-generation issue** (usings/references/DI/wiring/packages): attempt one focused fix pass, rebuild.
- **Deterministic mechanical cascade** (rename ripple, namespace cascade, file-move fallout from a single root cause): one extra pass allowed *only when* the second pass purely propagates the same fix and introduces no new logic. If the second pass surfaces new failure modes, stop and write the blocker.
- **Missing package in `Directory.Packages.props`**: add at latest stable version, restore, rebuild.
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access): do not loop fixes. Document the blocker in `HANDOFF.md`, point the engineer to [execution-gates.md](execution-gates.md).

Rule: one fix pass for new errors; one extra pass allowed for mechanical propagation of the same root-cause fix. Otherwise, write the blocker.

## Git Checkpoint Protocol

Create a checkpoint after each successful sub-phase gate.

- Prefer a `git commit` when the developer has approved committing.
- Otherwise record the exact suggested commit command in `HANDOFF.md` and ensure the working tree state is clearly described.
- Do not run destructive git commands (`reset --hard`, `checkout .`, `branch -D`, `push --force`) without explicit developer approval.

If a sub-phase fails after the one-pass fix attempt:

- isolate the broken changes from the last clean state,
- log the blocker in `HANDOFF.md`,
- continue only with non-blocked work.

Mid-session checkpoint trigger: 15+ generated files **or** 3+ build/fix cycles. Update `HANDOFF.md` immediately — do not wait for the gate.

## Missing-Inputs Protocol

When domain inputs are absent or ambiguous:

- **Required** (`ProjectName`, `customNugetFeeds`, at least one entity): ask before proceeding.
- **Defaults** (modes/profiles/flags): use [../ai/resource-implementation-schema.md](../ai/resource-implementation-schema.md) **Canonical Defaults**; note assumptions inline.
- **Partial entity definitions**: scaffold what is defined; emit `// TODO` stubs for missing properties/rules.

### Phase 3 Pre-Flight: Custom NuGet Feeds

At Phase 3 start: ask for custom/private NuGet feed URLs and auth method. Update `nuget.config`, run `dotnet restore`, and require exit code 0 before Phase 4.

## Mid-Session Rollback Protocol

When the AI discovers a fundamental assumption error (wrong entity design, incorrect relationship, misunderstood domain rule) after multiple files have been generated:

1. **Stop generating.** Do not continue building on a flawed assumption.
2. **Assess scope:** Identify all files generated since the last git checkpoint that are affected by the error.
3. **Checkpoint recovery prep:**
   ```powershell
   git stash          # save current work
   git log --oneline -5  # find last clean checkpoint
   ```
4. **Decide recovery path:**
   - **Isolated error** (affects 1-2 files): `git stash pop`, fix the affected files, rebuild and re-test.
   - **Structural error** (wrong entity shape, missing relationship, bad inheritance): ask the developer before discarding work. Prefer `git stash branch recovery/<short-name>` or a targeted revert over `git checkout <last-checkpoint>`.
   - **Domain misunderstanding** (entity purpose is wrong): go back to Phase 1 output, clarify with the user, update `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md`, then re-scaffold the slice from scratch.
5. **Document in HANDOFF.md:** Record what was rolled back and why, so the next session doesn't repeat the mistake.
6. **Re-enter at the corrected sub-phase** using the Phase 5 file table in `ai/SKILL.md`.

> **Rule:** Never patch-fix a structural error across 3+ files. It is faster and safer to rollback and re-scaffold than to chase cascading fixes.

## Mixed-Store Slice Gate

For slices spanning SQL + Cosmos/Table/Blob + messaging:

- Explicit consistency boundary (authoritative store + projection store).
- Reconciliation handler/job with replay-safe correction logic.
- Drift detection check in post-generation verification.

Until all three are present, the slice is incomplete regardless of `dotnet build` / `dotnet test` status.

## Context Budgets (advisory)

Target ≤30K tokens of instruction context per Phase 5 sub-phase, ≤16K in compact-mode harnesses. On 200K+ context models with no harness constraint, treat as a focus signal — load the curated set in the **Phase 5 file table** in `ai/SKILL.md`, add files only when the current sub-phase clearly needs them. Lost-in-the-middle is real even at 200K, so loading every skill hurts output quality.
