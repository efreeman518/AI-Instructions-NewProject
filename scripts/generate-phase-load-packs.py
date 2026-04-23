#!/usr/bin/env python3
"""Generate phase-load-packs.json from _manifest.json."""

import argparse
import json
import sys
from collections import OrderedDict
from pathlib import Path


SLICE_DEFINITIONS = OrderedDict([
    ("phase-5a", OrderedDict([
        ("domain", [
            "skills/domain-model.md",
            "templates/entity-template.md",
            "templates/domain-rules-template.md",
            "templates/test-templates-domain.md",
        ]),
        ("repository", [
            "skills/domain-model.md",
            "skills/data-persistence.md",
            "templates/entity-template.md",
            "templates/ef-configuration-template.md",
            "templates/repository-template.md",
            "templates/test-templates-repository.md",
        ]),
    ])),
    ("phase-5b", OrderedDict([
        ("service", [
            "skills/application-layer.md",
            "skills/bootstrapper.md",
            "templates/data-mapping-template.md",
            "templates/service-template.md",
            "templates/structure-validator-template.md",
            "templates/test-templates-service.md",
        ]),
        ("endpoint", [
            "skills/application-layer.md",
            "skills/bootstrapper.md",
            "skills/api.md",
            "templates/endpoint-template.md",
            "templates/exception-handler-template.md",
            "templates/test-templates-endpoint.md",
        ]),
    ])),
])


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


def validate_slice_definitions(slice_definitions, manifest_file_set, grouped):
    validated = OrderedDict()

    for phase, slices in slice_definitions.items():
        if phase not in grouped:
            continue

        phase_slices = OrderedDict()

        for slice_name, paths in slices.items():
            deduped = list(dict.fromkeys(paths))
            if any(path not in manifest_file_set for path in deduped):
                continue
            phase_slices[slice_name] = deduped

        if phase_slices:
            validated[phase] = phase_slices

    return validated


def get_mode_slice_pack(base_slices, excluded_skills):
    result = OrderedDict()
    for phase, slices in base_slices.items():
        phase_result = OrderedDict()
        for slice_name, paths in slices.items():
            if excluded_skills:
                filtered = [p for p in paths if p not in excluded_skills]
            else:
                filtered = list(paths)
            phase_result[slice_name] = filtered
        result[phase] = phase_result
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
        "phase-4",
        "phase-5-base",
        "phase-5a",
        "phase-5a-optional",
        "phase-5b",
        "phase-5c",
        "phase-5d",
        "phase-5d-optional",
        "phase-5e",
        "phase-5e-optional",
        "phase-5f",
        "phase-5g",
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

    phase_overlays = OrderedDict([
        ("phase-4", [
            "ai/placeholder-tokens.md",
            "support/ef-packages-reference.md",
        ]),
    ])
    for phase, overlay_paths in phase_overlays.items():
        if phase not in grouped:
            grouped[phase] = []
        for path in overlay_paths:
            if path not in manifest_file_set:
                raise SystemExit(
                    f"Phase overlay '{path}' for phase '{phase}' does not exist in manifest.files."
                )
            if path not in grouped[phase]:
                grouped[phase].append(path)

    base_slices = validate_slice_definitions(SLICE_DEFINITIONS, manifest_file_set, grouped)

    full_pack = get_mode_pack(grouped, [])
    lite_pack = get_mode_pack(grouped, excluded_lite)
    api_only_pack = get_mode_pack(grouped, excluded_api_only)

    full_slices = get_mode_slice_pack(base_slices, [])
    lite_slices = get_mode_slice_pack(base_slices, excluded_lite)
    api_only_slices = get_mode_slice_pack(base_slices, excluded_api_only)

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
        ("slices", OrderedDict([
            ("full", full_slices),
            ("lite", lite_slices),
            ("api-only", api_only_slices),
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
