#!/usr/bin/env python3
"""Generate phase-load-packs.json from _manifest.json."""

import argparse
import json
import sys
from collections import OrderedDict
from pathlib import Path


def get_mode_exclusions(manifest, mode, manifest_file_set):
    mode_exclusions = manifest.get("modeExclusions", {})
    if mode not in mode_exclusions:
        raise SystemExit(f"Manifest modeExclusions is missing '{mode}'.")

    paths = sorted(set(
        p for p in mode_exclusions[mode]
        if p and p.strip()
    ))

    for path in paths:
        if path not in manifest_file_set:
            raise SystemExit(
                f"Mode exclusion '{path}' for mode '{mode}' does not exist in manifest.files."
            )

    return paths


def get_mode_pack(base, excluded_skills):
    result = OrderedDict()
    for phase, paths in base.items():
        if excluded_skills:
            filtered = [p for p in paths if p not in excluded_skills]
        else:
            filtered = list(paths)
        result[phase] = filtered
    return result


def main():
    parser = argparse.ArgumentParser(description="Generate phase-load-packs.json from _manifest.json")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    parser.add_argument("--output-file", "-OutputFile", default="phase-load-packs.json",
                        help="Output filename (default: phase-load-packs.json)")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent
    output_file = args.output_file

    manifest_path = root / "_manifest.json"
    output_path = root / output_file

    if not manifest_path.exists():
        raise SystemExit(f"Manifest not found: {manifest_path}")

    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    if "modeExclusions" not in manifest:
        raise SystemExit("Manifest is missing top-level modeExclusions metadata.")

    manifest_file_set = {str(entry["path"]) for entry in manifest["files"]}

    excluded_lite = get_mode_exclusions(manifest, "lite", manifest_file_set)
    excluded_api_only = get_mode_exclusions(manifest, "api-only", manifest_file_set)

    phase_order = [
        "metadata",
        "session-bootstrap",
        "phase-1",
        "phase-2",
        "phase-3",
        "phase-4-base",
        "phase-4a",
        "phase-4a-optional",
        "phase-4b",
        "phase-4c",
        "phase-4d",
        "phase-4d-optional",
        "phase-4e",
        "phase-4e-optional",
        "phase-4f",
        "phase-4g",
        "support-only",
        "on-demand",
    ]

    grouped = OrderedDict()
    for phase in phase_order:
        grouped[phase] = []

    for entry in manifest["files"]:
        phase = str(entry["phase"])
        if phase not in grouped:
            grouped[phase] = []
        grouped[phase].append(str(entry["path"]))

    # Deduplicate per phase
    for phase in grouped:
        grouped[phase] = list(dict.fromkeys(grouped[phase]))

    full_pack = get_mode_pack(grouped, [])
    lite_pack = get_mode_pack(grouped, excluded_lite)
    api_only_pack = get_mode_pack(grouped, excluded_api_only)

    output = OrderedDict([
        ("source", OrderedDict([
            ("manifest", "_manifest.json"),
            ("generator", "scripts/generate-phase-load-packs.py"),
        ])),
        ("contextBudget", manifest["contextBudget"]),
        ("phaseOrder", phase_order),
        ("modeExclusions", OrderedDict([
            ("lite", excluded_lite),
            ("api-only", excluded_api_only),
        ])),
        ("packs", OrderedDict([
            ("full", full_pack),
            ("lite", lite_pack),
            ("api-only", api_only_pack),
        ])),
    ])

    json_str = json.dumps(output, indent=4, ensure_ascii=False)
    with open(output_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json_str)

    print(f"Generated {output_file}")


if __name__ == "__main__":
    main()
