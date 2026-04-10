#!/usr/bin/env python3
"""Resolve the load set for a given phase, mode, and feature flags."""

import argparse
import json
import subprocess
import sys
from pathlib import Path


def filter_requested_paths(paths, requested_phase, requested_mode,
                           include_gateway, include_function_app,
                           include_scheduler, include_uno_ui,
                           include_aspire, include_ai_services,
                           include_ai_search, include_agents):
    filtered = list(paths)

    if requested_phase == "phase-5c":
        if not include_gateway:
            filtered = [p for p in filtered if p != "skills/gateway.md"]
        if requested_mode == "api-only" and not include_aspire:
            filtered = [p for p in filtered if p != "skills/aspire.md"]

    if requested_phase == "phase-5d":
        if not include_scheduler:
            filtered = [p for p in filtered if p != "skills/background-services.md"]
        if not include_function_app:
            filtered = [p for p in filtered if p != "skills/function-app.md"]
        if not include_uno_ui:
            filtered = [p for p in filtered if p != "skills/uno-ui.md"]

    if requested_phase == "phase-5d-optional" and not include_uno_ui:
        return []

    if requested_phase == "phase-5g" and not include_ai_services:
        return []

    if requested_phase == "phase-5g" and include_ai_services:
        has_granular_ai_selection = include_ai_search or include_agents
        if has_granular_ai_selection:
            allowed_paths = {"skills/ai-integration.md"}
            if include_ai_search:
                allowed_paths.add("templates/ai-search-template.md")
            if include_agents:
                allowed_paths.add("templates/agent-template.md")
            filtered = [p for p in filtered if p in allowed_paths]

    return filtered


def get_entry_dependencies(entry):
    dependencies = []
    if "dependencies" in entry and entry["dependencies"] is not None:
        dependencies.extend(entry["dependencies"])
    if "requires" in entry and entry["requires"] is not None:
        dependencies.extend(entry["requires"])
    return list(dict.fromkeys(d for d in dependencies if d and d.strip()))


def add_resolved_path(path, visited, visiting, ordered_paths,
                      entry_map, mode_exclusion_set, mode):
    if path in visited:
        return
    if path in visiting:
        raise SystemExit(f"Dependency cycle detected while resolving '{path}'.")
    if path not in entry_map:
        raise SystemExit(f"Dependency '{path}' is not present in _manifest.json.")
    if path in mode_exclusion_set:
        raise SystemExit(
            f"Dependency '{path}' is excluded by mode '{mode}'. "
            "Adjust modeExclusions or the dependency graph."
        )

    visiting.add(path)
    for dep in get_entry_dependencies(entry_map[path]):
        add_resolved_path(str(dep), visited, visiting, ordered_paths,
                          entry_map, mode_exclusion_set, mode)
    visiting.discard(path)
    visited.add(path)
    ordered_paths.append(path)


def main():
    parser = argparse.ArgumentParser(description="Resolve the load set for a phase")
    parser.add_argument("--phase", "-Phase", required=True, help="Phase name (e.g. phase-5a)")
    parser.add_argument("--mode", "-Mode", default="full",
                        choices=["full", "lite", "api-only"], help="Scaffold mode")
    parser.add_argument("--include-gateway", "-IncludeGateway",
                        action="store_true", default=False)
    parser.add_argument("--include-function-app", "-IncludeFunctionApp",
                        action="store_true", default=False)
    parser.add_argument("--include-scheduler", "-IncludeScheduler",
                        action="store_true", default=False)
    parser.add_argument("--include-uno-ui", "-IncludeUnoUI",
                        action="store_true", default=False)
    parser.add_argument("--include-aspire", "-IncludeAspire",
                        action="store_true", default=False)
    parser.add_argument("--include-ai-services", "-IncludeAiServices",
                        action="store_true", default=False)
    parser.add_argument("--include-ai-search", "-IncludeAiSearch",
                        action="store_true", default=False)
    parser.add_argument("--include-agents", "-IncludeAgents",
                        action="store_true", default=False)
    parser.add_argument("--budget-profile", "-BudgetProfile", default="default",
                        choices=["default", "extended", "compact"],
                        help="Context budget profile")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    parser.add_argument("--as-json", "-AsJson", action="store_true", default=False,
                        help="Output as JSON")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent
    phase = args.phase if args.phase.startswith("phase-") else f"phase-{args.phase}"
    mode = args.mode
    budget_profile = args.budget_profile
    as_json = args.as_json

    packs_path = root / "phase-load-packs.json"
    manifest_path = root / "_manifest.json"

    if not packs_path.exists():
        gen_script = root / "scripts" / "generate-phase-load-packs.py"
        subprocess.run(
            [sys.executable, str(gen_script), "--root", str(root)],
            check=True, capture_output=True
        )

    with open(packs_path, "r", encoding="utf-8") as f:
        packs = json.load(f)
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    if phase not in packs["phaseOrder"]:
        raise SystemExit(f"Unknown phase '{phase}'.")

    mode_pack = packs["packs"].get(mode)
    if not mode_pack:
        raise SystemExit(f"Unknown mode '{mode}' in phase-load-packs.json.")

    if budget_profile not in packs["contextBudget"]:
        raise SystemExit(f"Unknown budget profile '{budget_profile}'.")

    raw_paths = mode_pack.get(phase, [])
    requested_paths = filter_requested_paths(
        raw_paths, phase, mode,
        args.include_gateway, args.include_function_app,
        args.include_scheduler, args.include_uno_ui,
        args.include_aspire, args.include_ai_services,
        args.include_ai_search, args.include_agents,
    )

    entry_map = {}
    for entry in manifest["files"]:
        entry_map[str(entry["path"])] = entry

    mode_exclusion_set = set()
    mode_exclusions = packs.get("modeExclusions", {})
    if mode in mode_exclusions:
        for path in mode_exclusions[mode]:
            mode_exclusion_set.add(str(path))

    ordered_paths = []
    visited = set()
    visiting = set()
    requested_set = set(str(p) for p in requested_paths)

    for path in requested_paths:
        add_resolved_path(str(path), visited, visiting, ordered_paths,
                          entry_map, mode_exclusion_set, mode)

    token_map = {}
    for entry in manifest["files"]:
        token_map[str(entry["path"])] = int(entry["estimatedTokens"])

    budget_limit = int(packs["contextBudget"][budget_profile])
    total_tokens = 0
    items = []

    for path in ordered_paths:
        tokens = token_map.get(path, 0)
        total_tokens += tokens
        items.append({
            "path": path,
            "phase": str(entry_map[path]["phase"]),
            "selection": "requested" if path in requested_set else "dependency",
            "estimatedTokens": tokens,
        })

    within_budget = total_tokens <= budget_limit

    if as_json:
        output = {
            "phase": phase,
            "mode": mode,
            "budgetProfile": budget_profile,
            "budgetLimit": budget_limit,
            "withinBudget": within_budget,
            "totalEstimatedTokens": total_tokens,
            "requestedFiles": [i for i in items if i["selection"] == "requested"],
            "dependencyFiles": [i for i in items if i["selection"] == "dependency"],
            "files": items,
        }
        print(json.dumps(output, indent=4))
        sys.exit(0)

    print(f"Phase: {phase}")
    print(f"Mode: {mode}")
    print(f"BudgetProfile: {budget_profile}")
    print(f"BudgetLimit: {budget_limit}")
    print(f"WithinBudget: {within_budget}")
    print(f"TotalEstimatedTokens: {total_tokens}")

    if not items:
        print("No files selected for this phase with current feature flags.")
        sys.exit(0)

    # Table output
    col_path = max(len(i["path"]) for i in items)
    col_phase = max(len(i["phase"]) for i in items)
    col_sel = max(len(i["selection"]) for i in items)
    col_tok = max(len(str(i["estimatedTokens"])) for i in items)

    col_path = max(col_path, len("path"))
    col_phase = max(col_phase, len("phase"))
    col_sel = max(col_sel, len("selection"))
    col_tok = max(col_tok, len("estimatedTokens"))

    header = (f"{'path':<{col_path}}  {'phase':<{col_phase}}  "
              f"{'selection':<{col_sel}}  {'estimatedTokens':>{col_tok}}")
    print()
    print(header)
    print("-" * len(header))
    for item in items:
        print(f"{item['path']:<{col_path}}  {item['phase']:<{col_phase}}  "
              f"{item['selection']:<{col_sel}}  {item['estimatedTokens']:>{col_tok}}")
    print()

    if not within_budget:
        overage = total_tokens - budget_limit
        print(f"WARNING: Selected load set exceeds the {budget_profile} budget by {overage} tokens.",
              file=sys.stderr)


if __name__ == "__main__":
    main()
