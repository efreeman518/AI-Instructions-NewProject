# UPDATE-INSTRUCTIONS.md

> **Purpose:** As the scaffolding agent follows the instructions in this repository, it will encounter gaps, ambiguities, outdated patterns, or better approaches that aren't captured in the current skill/template/instruction files. Rather than modifying instruction files mid-scaffolding, the agent appends findings here. A separate **instruction maintenance agent** reads this file and applies approved changes to the baseline instructions.

## How This File Works

### For the Scaffolding Agent (writer)

- **Append findings as you encounter them** — don't wait until the end of a session.
- **Don't modify instruction files directly** during scaffolding. Capture the finding here instead.
- Each finding should be **actionable** — reference specific file paths, section names, and line ranges when possible.
- Use the priority levels to help the maintenance agent triage work.
- **During MCP discovery** (before each phase), log any new, deprecated, or changed MCP servers here so the maintenance agent can update the static MCP tables in `SKILL.md`. Tag MCP findings with `[MCP]` in the title.

### For the Instruction Maintenance Agent (reader)

Your job is to read the findings below and apply approved changes to the instruction files in this repository. Follow these rules:

1. **Read all findings** before making changes — some may conflict or supersede each other.
2. **Group related findings** — multiple findings about the same file/section should be applied as a single coherent edit.
3. **Preserve existing style and tone** — match the voice, formatting, and structure of the target file.
4. **Update cross-references** — if a change in one file affects references in other files (e.g., adding a new skill creates a new row in the skill table in `SKILL.md`), update all references.
5. **Validate consistency** — after making changes, check that the modified instructions don't contradict other instruction files.
6. **Mark findings as applied** — after applying a finding, change its status from `pending` to `applied` and add the date.
7. **Reject with reason** — if a finding is incorrect or not applicable, change its status to `rejected` with a brief explanation.

### Priority Levels

| Priority | Meaning | Maintenance SLA |
|----------|---------|-----------------|
| `critical` | Instructions produce code that doesn't compile or breaks at runtime | Apply immediately |
| `high` | Missing pattern causes significant rework or confusion | Apply before next scaffolding session |
| `medium` | Improvement that would save time or reduce ambiguity | Apply when convenient |
| `low` | Nice-to-have refinement, style improvement, or edge case | Batch with other low-priority items |

---

## Findings

<!-- 
### {Finding Title}
- **Status:** pending | applied (YYYY-MM-DD) | rejected (reason)
- **File(s) to update:** `skills/{file}.md`, `templates/{file}.md`
- **Section:** {section name or heading within the file}
- **Current behavior:** {what the instructions say or omit}
- **Recommended change:** {what should be added, changed, or removed — be specific}
- **Reason:** {why this improves future scaffolding}
- **Priority:** critical | high | medium | low
- **Discovered during:** {which phase or task surfaced this finding}

### [MCP] {MCP Finding Title}
- **Status:** pending | applied (YYYY-MM-DD) | rejected (reason)
- **File(s) to update:** `SKILL.md` (Recommended MCP Servers section)
- **MCP server:** {package name or URL}
- **Action:** add | update | deprecate | remove
- **Covers:** {what libraries/services/phases this MCP is relevant to}
- **Install/config:** {install command or config snippet}
- **Current instructions:** {what the MCP tables currently say, or "not listed"}
- **Recommended change:** {add to Essential/Recommended/Optional tier, update description, etc.}
- **Reason:** {why this MCP improves the scaffolding workflow}
- **Priority:** medium | low
- **Discovered during:** {phase name — e.g., "Phase 4 MCP discovery before Uno UI scaffolding"}
-->

<!-- No pending findings. -->
