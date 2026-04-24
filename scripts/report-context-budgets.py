#!/usr/bin/env python3
"""Report hot-path instruction budgets for core phases and compact slices."""

import argparse
import json
import sys
from pathlib import Path


CORE_PHASES = ["phase-4", "phase-5-base"]
RESOLVED_PHASES = [
    "phase-4",
    "phase-5a",
    "phase-5b",
    "phase-5c",
    "phase-5d",
    "phase-5e",
    "phase-5f",
    "phase-5g",
]
COMPACT_SLICES = {
    "phase-5a": ["domain", "repository"],
    "phase-5b": ["service", "endpoint"],
}


def sum_paths(paths, token_by_path, label):
    missing = [path for path in paths if path not in token_by_path]
    if missing:
        missing_list = ", ".join(missing)
        raise SystemExit(f"{label} references paths missing from _manifest.json: {missing_list}")
    return sum(token_by_path[path] for path in paths)


def build_budget_flags(total_tokens, budgets):
    return {
        "withinCompact": total_tokens <= budgets["compact"],
        "withinDefault": total_tokens <= budgets["default"],
        "withinExtended": total_tokens <= budgets["extended"],
    }


def get_dependencies(entry):
    dependencies = []
    dependencies.extend(entry.get("dependencies") or [])
    dependencies.extend(entry.get("requires") or [])
    return list(dict.fromkeys(str(dep) for dep in dependencies if str(dep).strip()))


def resolve_paths(paths, entry_by_path, mode_exclusions, expand_dependencies=True):
    ordered = []
    visited = set()
    visiting = set()

    def add(path):
        if path in visited:
            return
        if path in visiting:
            raise SystemExit(f"Dependency cycle detected while resolving '{path}'.")
        if path not in entry_by_path:
            raise SystemExit(f"Load set references path missing from _manifest.json: {path}")
        if path in mode_exclusions:
            return

        visiting.add(path)
        if expand_dependencies:
            for dep in get_dependencies(entry_by_path[path]):
                add(dep)
        visiting.remove(path)
        visited.add(path)
        ordered.append(path)

    for path in paths:
        add(str(path))

    return ordered


def main():
    parser = argparse.ArgumentParser(description="Report core phase and compact slice context budgets")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    parser.add_argument("--mode", "-Mode", default="full",
                        choices=["full", "lite", "api-only"],
                        help="Scaffold mode to report")
    parser.add_argument("--as-json", "-AsJson", action="store_true", default=False,
                        help="Output as JSON")
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent
    packs_path = root / "phase-load-packs.json"
    manifest_path = root / "_manifest.json"

    with packs_path.open("r", encoding="utf-8") as handle:
        packs = json.load(handle)
    with manifest_path.open("r", encoding="utf-8") as handle:
        manifest = json.load(handle)

    budgets = packs["contextBudget"]
    mode_packs = packs["packs"].get(args.mode)
    mode_slices = packs.get("slices", {}).get(args.mode, {})
    if mode_packs is None:
        raise SystemExit(f"Unknown mode '{args.mode}' in phase-load-packs.json.")

    token_by_path = {
        str(entry["path"]): int(entry.get("estimatedTokens", 0))
        for entry in manifest["files"]
    }
    entry_by_path = {
        str(entry["path"]): entry
        for entry in manifest["files"]
    }
    mode_exclusions = set(str(path) for path in packs.get("modeExclusions", {}).get(args.mode, []))

    core_phases = {}
    for phase in CORE_PHASES:
        paths = mode_packs.get(phase, [])
        total_tokens = sum_paths(paths, token_by_path, phase)
        core_phases[phase] = {
            "files": paths,
            "totalEstimatedTokens": total_tokens,
            **build_budget_flags(total_tokens, budgets),
        }

    slice_totals = {}
    for phase, slice_names in COMPACT_SLICES.items():
        phase_slices = mode_slices.get(phase, {})
        current = {}
        for slice_name in slice_names:
            paths = phase_slices.get(slice_name)
            if paths is None:
                raise SystemExit(f"Missing compact slice '{args.mode}:{phase}:{slice_name}' in phase-load-packs.json.")
            total_tokens = sum_paths(paths, token_by_path, f"{args.mode}:{phase}:{slice_name}")
            current[slice_name] = {
                "files": paths,
                "totalEstimatedTokens": total_tokens,
                **build_budget_flags(total_tokens, budgets),
            }
        slice_totals[phase] = current

    resolved_phase_totals = {}
    for phase in RESOLVED_PHASES:
        paths = mode_packs.get(phase, [])
        resolved_paths = resolve_paths(
            paths,
            entry_by_path,
            mode_exclusions,
            expand_dependencies=(phase != "phase-5d"),
        )
        total_tokens = sum_paths(resolved_paths, token_by_path, phase)
        resolved_phase_totals[phase] = {
            "files": resolved_paths,
            "totalEstimatedTokens": total_tokens,
            **build_budget_flags(total_tokens, budgets),
        }

    output = {
        "mode": args.mode,
        "contextBudget": budgets,
        "corePhases": core_phases,
        "sliceTotals": slice_totals,
        "resolvedPhaseTotals": resolved_phase_totals,
    }

    if args.as_json:
        print(json.dumps(output, indent=4))
        return

    print(f"Mode: {args.mode}")
    print(
        f"Budgets: compact={budgets['compact']} default={budgets['default']} extended={budgets['extended']}"
    )
    print("Core phases:")
    for phase, info in core_phases.items():
        print(
            f"  {phase}: {info['totalEstimatedTokens']} "
            f"(compact={info['withinCompact']}, default={info['withinDefault']}, extended={info['withinExtended']})"
        )

    print("Compact slices:")
    for phase, slices in slice_totals.items():
        for slice_name, info in slices.items():
            print(
                f"  {phase}:{slice_name}: {info['totalEstimatedTokens']} "
                f"(compact={info['withinCompact']}, default={info['withinDefault']}, extended={info['withinExtended']})"
            )

    print("Resolved phase load sets (unfiltered feature flags):")
    for phase, info in resolved_phase_totals.items():
        print(
            f"  {phase}: {info['totalEstimatedTokens']} "
            f"(compact={info['withinCompact']}, default={info['withinDefault']}, extended={info['withinExtended']})"
        )


if __name__ == "__main__":
    main()
