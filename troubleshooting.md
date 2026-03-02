# Troubleshooting — AI Agent Triage Rules

This file is intentionally lightweight. Use it to decide **what the AI should do next** when a build/test/run problem appears.

> **For compile/run commands and engineer actions, use [engineer-checklist.md](engineer-checklist.md) as the execution checklist.**

---

## Core Rule

AI agents generate code. Engineers own environment and runtime setup.

> **⛔ NEVER fix, modify, or build sampleapp code unless explicitly instructed to do so.** The `sample-app/` directory is read-only reference. All code generation and fixes apply to the **new project** only. If an error points to a sampleapp file, document the issue and fix only in `UPDATE-INSTRUCTIONS.md` — it is not your code to fix directly.

When an error appears:
1. Classify it (code-generation vs infrastructure/tooling)
2. Attempt **one** code-fix pass only when it is code-generation **in the new project**
3. If still failing (or infrastructure-related), log in `HANDOFF.md` and continue with non-blocked work

---

## AI Fixes (One Pass Max)

The AI may fix:
- Missing `using`, `ProjectReference`, or package entries
- Missing DI registration, `DbSet<>`, endpoint map, or token substitution
- Namespace/path mismatches caused by scaffolding

Then run the phase validation command and continue only if green.

---

## Engineer-Owned Issues (Do Not Diagnose Deeply)

Flag immediately for the engineer when issues involve:
- NuGet auth/feeds, Docker/container startup, SQL connectivity
- Aspire env vars/ports, Functions runtime tools/config, Playwright install
- Certificates/credentials, cloud subscription access, deployment permissions

Reference the exact relevant section in [engineer-checklist.md](engineer-checklist.md).

---

## Domain Ambiguity Defaults

When inputs are unclear, prefer pragmatic defaults and continue:
- Relationship unclear → default to one-to-many and note assumption
- Missing properties → add `Name`; add `TenantId` when tenant-scoped
- Lite mode + Gateway requested → keep Lite baseline; suggest Gateway as a later increment

---

## Common Test Failures

Use [test-gotchas.md](test-gotchas.md) for the canonical test failure catalog and fixes.

---

## Session State

When blocked, log in `HANDOFF.md` (see [template](HANDOFF.md)):
- Symptom + classification (`code-generation` or `infrastructure`)
- Current phase
- Next engineer action (link to [engineer-checklist.md](engineer-checklist.md))

If instruction gaps are discovered, append to `UPDATE-INSTRUCTIONS.md`.
