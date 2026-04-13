#!/usr/bin/env python3
"""Report hot-path instruction budgets for core phases and compact slices."""

import argparse
import json
import sys
from pathlib import Path


CORE_PHASES = ["phase-4", "phase-5-base"]
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

    output = {
        "mode": args.mode,
        "contextBudget": budgets,
        "corePhases": core_phases,
        "sliceTotals": slice_totals,
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


if __name__ == "__main__":
    main()