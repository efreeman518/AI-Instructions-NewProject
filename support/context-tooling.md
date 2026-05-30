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

Both graph tools are installed/updated globally by
`misc/update-python-and-context-tools.ps1`. That script installs the CLIs only. It
does not enable any repo harness and does not create a graph database.
Per-harness enablement and per-repo graph creation are project-time actions.

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

Global CLI install:

```powershell
winget install astral-sh.uv    # only if uv is missing
uv tool install graphifyy      # PyPI package name has double-y
uv tool upgrade graphifyy      # update an existing global install
graphify --version
```

The CLI command is `graphify`. Avoid plain `pip install graphifyy` on Windows and
Mac unless there is no alternative; Graphify's own guidance prefers `uv tool`
or `pipx` to avoid interpreter mismatch during skill execution.

Enable Graphify per repo and per harness only where wanted. Run from the target
repo root after the global CLI exists:

```powershell
graphify claude install --project
graphify codex install --project
graphify copilot install --project
```

Equivalent generic form:

```powershell
graphify install --project --platform codex
```

Codex also needs `multi_agent = true` under `[features]` in
`%USERPROFILE%\.codex\config.toml` before `$graphify` skill commands are
available. If skill commands are unavailable, use the CLI commands below.

Create the graph database from the repo root:

```powershell
graphify .              # PowerShell CLI: no leading slash
```

Verify the build created:

- `graphify-out/graph.json`
- `graphify-out/GRAPH_REPORT.md`
- `graphify-out/graph.html`

Do not treat a global install or project harness registration as a built graph.
The repo is not graph-enabled until `graphify-out/graph.json` exists.

Query or refresh an existing graph:

```powershell
graphify query "what connects the API to persistence?"
graphify . --update
graphify extract . --force    # use after large refactors or stale/duplicate nodes
```

Codex skill syntax is `$graphify .`; Claude-style `/graphify .` is not valid in
PowerShell. Prefer CLI/skill mode over the Graphify MCP server to avoid standing
tool-schema tokens.

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
src/Infrastructure/**/Migrations/

Keep `.instructions/`, `.scaffold/`, `docs/*.md`, `HANDOFF.md`, all `src/*.cs|.razor`.
If committing the graph into a template, add to `.gitignore`:
`graphify-out/manifest.json` and `graphify-out/cost.json` (mtime-based, break on clone).

## Setup - codegraph (code-heavy repos)

Global CLI install:

```powershell
npm install -g @colbymchenry/codegraph
codegraph --version
```

Upstream also publishes a bundled Windows installer that does not require Node:

```powershell
irm https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.ps1 | iex
```

The scaffold update script uses npm because Node/npm are already part of the
context-tooling stack. If npm is missing, install Node 20+ or use the upstream
PowerShell installer.

Enable CodeGraph for supported harnesses only where wanted. CodeGraph configures
an MCP server; it does not write persistent usage instructions to `CLAUDE.md` or
`AGENTS.md` because the MCP server sends guidance in its initialize response.

```powershell
codegraph install --target=claude --location=local --yes
codegraph install --target=codex --location=global --yes
codegraph install --target=claude,cursor,opencode --location=local --yes
codegraph install --print-config codex
```

Accuracy notes:

- Claude supports project-local config through `.mcp.json`.
- Codex CLI is global-only in current CodeGraph builds; upstream reports no
  project-local Codex config support, so `--location=local` skips Codex.
- GitHub Copilot is not in the current CodeGraph installer target registry.
  Do not invent a Copilot setup path unless upstream adds one.

Create the index database from the repo root:

```powershell
codegraph init -i
```

Verify the build created `.codegraph/codegraph.db`:

```powershell
codegraph status
```

Do not treat global CLI install or MCP harness registration as an indexed repo.
The repo is not CodeGraph-enabled until `.codegraph/codegraph.db` exists.

Refresh and query:

```powershell
codegraph sync
codegraph index --force
codegraph query UserService
codegraph context "trace request flow to persistence"
```

CodeGraph is 100% local, AST-only, and no API key is required. Native C#/TS/JS
support exists, but CodeGraph does not index `.razor`, `.xaml`, markdown, or YAML.
That is why knowledge-heavy repos use graphify instead.

Add `.codegraph/` to `.gitignore` unless committing the index.

## Phase timing for scaffolded apps

Build/refresh the graph at phase boundaries, not continuously, to avoid churn during
the volatile Phase 4-5 window where code lands and artifacts get superseded:

- After Phase 1 artifacts exist, run `graphify .` if Graphify was selected.
- After Phase 4 (`dotnet build` green, `contractsScaffolded: true`), run `graphify . --update`.
- After a Phase 5 sub-phase gate passes, run `graphify . --update`.

Drift rule (per START-AI.md, Phase-1 Artifact Lifecycle Rule, and
support/OPERATIONS.md Mid-Session Rollback Protocol): when artifact and code
disagree, code wins - fix the artifact, then re-extract the affected slice.
