# Context Tooling - Per-Repo Knowledge Graph Selection

How to decide which knowledge-graph tool to use per repository, and how to wire it
in. Knowledge-graph tools reduce orientation token cost by letting an agent query
relationships instead of grepping/reading raw files. They sit upstream of the
compression tools (headroom, rtk) and do not overlap with them.

## Tool stack roles (no overlap)

- rtk - compresses CLI command output. Enforced via the `rtk` Bash prefix rule.
- headroom - compresses prompt inputs (tool outputs, history) before the API call.
- Output compression (caveman style) - enforced via instruction rules, not a tool.
- Knowledge graph (graphify OR codegraph) - reduces what gets loaded by enabling
  relationship queries. This file governs which one, per repo.

Pipeline: graph (what to load) -> headroom (compress inputs) -> rtk + output rules.

Both graph tools are installed/updated by `misc/update-python-and-context-tools.ps1`.
That script installs the binaries only. Per-repo registration and indexing (below)
are project-time actions, not update-time.

## Decision rule (LOC ratio)

Measure two sums, excluding generated/transient files (bin, obj, node_modules,
.tmp, TestResults, StrykerOutput, BenchmarkDotNet.Artifacts, logs,
package-lock.json, *.Designer.cs, ModelSnapshot.cs, rendered *.html docs):

- KNOWLEDGE = LOC of `.instructions/` + `.scaffold/` + `docs/*.md`
- CODE = LOC of application `src/` (*.cs, *.razor, *.ts, *.tsx, *.xaml)

| Condition                                                | Tool      | Why                                                                 |
|----------------------------------------------------------|-----------|---------------------------------------------------------------------|
| KNOWLEDGE >= CODE                                        | graphify  | Doc/spec layer is the majority; only graphify reads it              |
| CODE > KNOWLEDGE but CODE < 3x KNOWLEDGE                 | graphify  | Spec<->code links still high value; semantic layer wins             |
| CODE >= 3x KNOWLEDGE                                     | codegraph | Mostly code navigation; local + free + AST is sufficient            |
| No `.instructions/` or `.scaffold/`                      | codegraph | Plain code repo; no semantic doc layer to miss                      |
| Brownfield adoption pass (src/ exists, no .scaffold/ yet)| codegraph | Thick code, no knowledge layer yet; re-evaluate after Phase-1 artifacts derived |

Rationale: graphify uses tree-sitter for code PLUS LLM semantic extraction for
markdown/YAML/infra - it sees the whole repo. codegraph is AST-only, 100% local,
zero API cost, but blind to the `.instructions/` / `.scaffold/` / docs layer and to
`.razor` / `.xaml` markup. A freshly scaffolded app is knowledge-heavy
(KNOWLEDGE >= CODE) -> graphify. A mature app where code dwarfs the static doc
layer -> codegraph, with graphify reserved for periodic spec<->code consistency
checks.

## Measure command (PowerShell)

```powershell
$prune = '\\(node_modules|\.git|bin|obj|dist|\.tmp|TestResults|StrykerOutput|BenchmarkDotNet\.Artifacts|\.venv|packages)\\'
$skip  = '(package-lock\.json|.*\.Designer\.cs|.*ModelSnapshot\.cs|.*\.csproj\.user)$'
function Sum-Loc($paths) {
  ($paths | Where-Object { Test-Path $_ } | Get-ChildItem -Recurse -File -EA SilentlyContinue |
    Where-Object { $_.FullName -notmatch $prune -and $_.Name -notmatch $skip } |
    ForEach-Object { (Get-Content -LiteralPath $_.FullName | Measure-Object -Line).Lines } |
    Measure-Object -Sum).Sum
}
$knowledge = Sum-Loc @('.instructions','.scaffold','docs')
$code = (Get-ChildItem src -Recurse -File -Include *.cs,*.razor,*.ts,*.tsx,*.xaml -EA SilentlyContinue |
         Where-Object { $_.FullName -notmatch $prune -and $_.Name -notmatch $skip } |
         ForEach-Object { (Get-Content -LiteralPath $_.FullName | Measure-Object -Line).Lines } |
         Measure-Object -Sum).Sum
"KNOWLEDGE=$knowledge  CODE=$code  ratio(code/knowledge)={0:N2}" -f ($code / [math]::Max($knowledge,1))
```

## Setup - graphify (knowledge-heavy repos)

Binary installed by the update script (PyPI `graphifyy` into an isolated venv at
`%USERPROFILE%\.graphify\runtime`; `graphify` shim on PATH).

    graphify install        # register skill with the agent (once per machine)
    graphify .              # build graph at repo root (PowerShell: no leading slash)

First build sends docs/YAML/markdown through the model. Add `.graphifyignore` (below).
Reuse across scaffolded apps: the `.instructions/` payload is identical per app, so
prefer `graphify . --update` to extract only changed `.scaffold/` deltas.
Prefer CLI/skill mode over the graphify MCP server (avoids standing tool-schema tokens).

`.graphifyignore` (repo root):
**/bin/
**/obj/
**/dist/
**/node_modules/
**/.tmp/
**/TestResults/
**/StrykerOutput/
**/BenchmarkDotNet.Artifacts/
**/.log
**/.trx
**/package-lock.json
/.csproj.user
docs/.html
docs/assets/
src/Infrastructure//Migrations/

Keep `.instructions/`, `.scaffold/`, `docs/*.md`, `HANDOFF.md`, all `src/*.cs|.razor`.
If committing the graph into a template, add to `.gitignore`:
`graphify-out/manifest.json` and `graphify-out/cost.json` (mtime-based, break on clone).

## Setup - codegraph (code-heavy repos)

Binary installed by the update script (npm global `@colbymchenry/codegraph`, Node 18+).
100% local, AST-only, no API key. Native C#/TS/JS support; does NOT index `.razor`,
`.xaml`, markdown, or YAML - that is why knowledge-heavy repos use graphify instead.

    codegraph init -i       # initialize + index current project (.codegraph/ created)
    codegraph status        # verify index

Add `.codegraph/` to `.gitignore` unless committing the index.

## Phase timing for scaffolded apps

Build/refresh the graph at phase boundaries, not continuously, to avoid churn during
the volatile Phase 4-5 window where code lands and artifacts get superseded:

- After Phase 1 (artifacts exist) -> first graph build, high value.
- After Phase 4 (`dotnet build` green, `contractsScaffolded: true`) -> refresh.
- After a Phase 5 sub-phase gate passes -> `graphify . --update`.

Drift rule (per START-AI.md, Phase-1 Artifact Lifecycle Rule, and
support/OPERATIONS.md Mid-Session Rollback Protocol): when artifact and code
disagree, code wins - fix the artifact, then re-extract the affected slice.