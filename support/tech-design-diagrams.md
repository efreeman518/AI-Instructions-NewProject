# Tech Design Diagrams — Source-Plus-Rendered Pattern

Canonical rules for diagrams that ship inside generated **GitHub-facing technical design docs** (e.g. `docs/tech-design.md`, `docs/tech-design.html`). GitHub's live Mermaid runtime rejects diagram variants the local Mermaid CLI accepts — C4, `block-beta`, complex ER, styled flowcharts, newer syntax. The fix is to commit both the source and the rendered asset.

## Scope

| Applies | Does not apply |
|---|---|
| `docs/tech-design.md`, `docs/tech-design.html`, and any peer **GitHub-rendered** architecture/topology docs the scaffold generates under `docs/` | Scaffold-internal artifacts under `.scaffold/` (e.g. `DESIGN-DECISIONS.md`, `implementation-plan.md`). These remain plain inline `mermaid` fences — they are working artifacts consumed by the AI, not GitHub-rendered docs. |

If a diagram source uses only basic `flowchart` / `sequenceDiagram` / `stateDiagram` with no styling and no `block-beta`/C4/`classDiagram-v2`, an inline fence in `docs/` is acceptable. When in doubt, render to SVG — round-trip cost is low and the GitHub render is then deterministic.

## Pattern

1. **Editable source** lives under `docs/assets/tech-design-diagrams/*.mmd` (one diagram per file).
2. **Rendered asset** is a matching `docs/assets/tech-design-diagrams/*.svg` checked into the repo.
3. **`docs/tech-design.md`** references the SVG, not a Mermaid fence:

   ```md
   <!-- Mermaid source: assets/tech-design-diagrams/05-service-topology.mmd -->
   ![Service Topology](assets/tech-design-diagrams/05-service-topology.svg)
   ```

4. **`docs/tech-design.html`** (when generated) references the same SVG:

   ```html
   <figure class="diagram">
     <img src="assets/tech-design-diagrams/05-service-topology.svg" alt="Service Topology" />
   </figure>
   ```

5. **Do not** include a Mermaid runtime in generated HTML:
   - no `mermaid@…` CDN `<script>`
   - no `class="mermaid"` blocks
   - no `mermaid.initialize(…)` call

## Stable Filenames

Use these names so cross-doc links remain valid as content evolves:

| File | Diagram |
|---|---|
| `01-system-context-diagram.{mmd,svg}` | C4 system context |
| `02-container-diagram.{mmd,svg}` | C4 container |
| `03-component-diagram-api.{mmd,svg}` | C4 component (API) |
| `04-software-architecture-layers.{mmd,svg}` | Layered architecture |
| `05-service-topology.{mmd,svg}` | Runtime/service topology |
| `06-domain-relationships.{mmd,svg}` | Domain relationships |
| `07-domain-entity-schema.{mmd,svg}` | Entity/ER schema |

Additional diagrams use the next available `08-…`, `09-…` index.

## Render Gate (scaffold time)

Run after creating or editing any `.mmd` file. Fails fast on the first diagram that does not render.

```powershell
rtk powershell -NoProfile -Command '& {
  $fail = @()
  Get-ChildItem "docs\assets\tech-design-diagrams\*.mmd" | Sort-Object Name | ForEach-Object {
    $out = [System.IO.Path]::ChangeExtension($_.FullName, ".svg")
    npx -y "@mermaid-js/mermaid-cli@10.9.1" -i $_.FullName -o $out -t dark -b "#0f1419" --quiet
    if ($LASTEXITCODE -ne 0) { $fail += $_.Name }
  }
  if ($fail.Count -eq 0) { "all ok" } else { $fail; exit 1 }
}'
```

Pin `@mermaid-js/mermaid-cli@10.9.1` so the rendered SVGs stay deterministic. Theme/background (`-t dark -b "#0f1419"`) is the scaffold default; override only if the project's `docs/` template uses a different theme.

## Final Doc Validation

Run before declaring the tech-design docs done. Both commands must exit clean.

```powershell
rtk rg -n -e '```mermaid' -e 'class="mermaid"' -e 'mermaid\.initialize' -e 'mermaid@' docs\tech-design.md docs\tech-design.html
rtk git diff --check
```

The first command must return no hits inside `docs/tech-design.md` or `docs/tech-design.html`. The second catches whitespace/CRLF damage on the SVG payload.

## Expected Result

- `docs/tech-design.md` renders cleanly on GitHub — no Mermaid parser failures, no "Unable to render rich display" banners.
- `.mmd` source remains editable; rerunning the render gate regenerates the `.svg` deterministically.
- Markdown and HTML stay synchronized because they reference the same SVG asset.
