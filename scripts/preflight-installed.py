#!/usr/bin/env python3
"""Validate an installed .instructions payload without mutating it.

This script is for consumer app repositories after the instruction set has been
copied into <app>/.instructions. It intentionally does not regenerate manifests,
does not require the author-side tests/ folder, and does not write files.
"""

import argparse
import json
import sys
from pathlib import Path


REQUIRED_INSTRUCTION_FILES = [
    "README.md",
    "CLAUDE.md",
    "START-AI.md",
    "_manifest.json",
    "phase-load-packs.json",
    "payload-manifest.json",
]

REQUIRED_INSTRUCTION_DIRS = [
    "ai",
    "patterns",
    "schemas",
    "skills",
    "support",
    "templates",
    "scripts",
]

REQUIRED_AGENT_FILES = [
    "AGENTS.md",
    ".claude/commands/scaffold.md",
    ".claude/commands/vertical-slice.md",
    ".github/agents/dotnet-scaffold.agent.md",
    ".github/agents/vertical-slice.agent.md",
]

REQUIRED_SCRIPT_FILES = [
    "scripts/configure-ef-packages-feed.py",
    "scripts/preflight-installed.py",
    "scripts/run-final-scaffold-check.py",
    "scripts/setup-local.py",
    "scripts/validate-domain-spec.py",
    "scripts/validate-ubiquitous-language.py",
    "scripts/validate-resource-impl.py",
    "scripts/validate-ef-packages-feed.py",
    "scripts/validate-handoff.py",
    "scripts/validate-implementation-plan.py",
    "scripts/validate-scaffold-output.py",
]


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def read_json(path, issues):
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except Exception as exc:
        add_issue(issues, "Json", path, f"Unable to parse JSON: {exc}")
        return None


def check_required_paths(root, app_root, instructions_only, issues):
    for rel in REQUIRED_INSTRUCTION_FILES:
        path = root / rel
        if not path.is_file():
            add_issue(issues, "Payload", path, "Required instruction file is missing.")

    for rel in REQUIRED_INSTRUCTION_DIRS:
        path = root / rel
        if not path.is_dir():
            add_issue(issues, "Payload", path, "Required instruction directory is missing.")

    for rel in REQUIRED_SCRIPT_FILES:
        path = root / rel
        if not path.is_file():
            add_issue(issues, "Payload", path, "Required validation script is missing.")

    if instructions_only:
        return

    for rel in REQUIRED_AGENT_FILES:
        path = app_root / rel
        if not path.is_file():
            add_issue(
                issues,
                "AgentPlacement",
                path,
                "Agent/command file is missing at the app repo root. "
                "Run install-to-project.py or rerun this script with --instructions-only.",
            )


def check_manifest_references(root, manifest, issues):
    if not manifest:
        return

    seen = set()
    for entry in manifest.get("files", []):
        rel = str(entry.get("path", ""))
        if not rel:
            add_issue(issues, "Manifest", "_manifest.json", "Manifest entry has no path.")
            continue
        if rel in seen:
            add_issue(issues, "Manifest", rel, "Duplicate manifest path.")
        seen.add(rel)

        path = root / rel
        if not path.exists():
            add_issue(issues, "Manifest", path, "Manifest references a missing file.")

        if "phase" not in entry:
            add_issue(issues, "Manifest", rel, "Manifest entry is missing phase.")
        if "estimatedTokens" not in entry:
            add_issue(issues, "Manifest", rel, "Manifest entry is missing estimatedTokens.")


def check_phase_load_packs(root, packs, manifest, issues):
    if not packs:
        return

    manifest_paths = {
        str(entry.get("path"))
        for entry in (manifest or {}).get("files", [])
        if entry.get("path")
    }

    for key in ["contextBudget", "phaseOrder", "packs"]:
        if key not in packs:
            add_issue(issues, "PhasePacks", "phase-load-packs.json", f"Missing key: {key}.")

    for mode in ["full", "lite", "api-only"]:
        mode_pack = packs.get("packs", {}).get(mode)
        if mode_pack is None:
            add_issue(issues, "PhasePacks", mode, "Missing scaffold mode pack.")
            continue

        for phase, paths in mode_pack.items():
            for rel in paths:
                if rel not in manifest_paths:
                    add_issue(
                        issues,
                        "PhasePacks",
                        rel,
                        f"{mode}:{phase} references a path not present in _manifest.json.",
                    )
                if not (root / rel).exists():
                    add_issue(
                        issues,
                        "PhasePacks",
                        rel,
                        f"{mode}:{phase} references a missing file.",
                    )


def print_issues(issues):
    if not issues:
        return

    issues.sort(key=lambda issue: (issue["category"], issue["path"], issue["message"]))
    width_category = max(len("Category"), *(len(issue["category"]) for issue in issues))
    width_path = max(len("Path"), *(len(issue["path"]) for issue in issues))

    print(f"{'Category':<{width_category}}  {'Path':<{width_path}}  Message")
    print("-" * (width_category + width_path + 11))
    for issue in issues:
        print(f"{issue['category']:<{width_category}}  {issue['path']:<{width_path}}  {issue['message']}")


def main():
    parser = argparse.ArgumentParser(
        description="Validate an installed AI instruction payload without modifying files.",
    )
    parser.add_argument(
        "--root",
        "-Root",
        default=None,
        help="Path to .instructions (defaults to parent of scripts/).",
    )
    parser.add_argument(
        "--app-root",
        "-AppRoot",
        default=None,
        help="Path to the consumer app root (defaults to parent of .instructions).",
    )
    parser.add_argument(
        "--instructions-only",
        action="store_true",
        default=False,
        help="Skip root-level .claude/.github agent placement checks.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent
    app_root = Path(args.app_root).resolve() if args.app_root else (
        root.parent if root.name == ".instructions" else root
    )

    issues = []
    if not root.exists():
        add_issue(issues, "Payload", root, "Instruction root does not exist.")
        print_issues(issues)
        return 1

    check_required_paths(root, app_root, args.instructions_only, issues)

    manifest = read_json(root / "_manifest.json", issues)
    packs = read_json(root / "phase-load-packs.json", issues)

    check_manifest_references(root, manifest, issues)
    check_phase_load_packs(root, packs, manifest, issues)

    if issues:
        print(f"FAIL: installed instruction preflight found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    print("PASS: installed instruction payload is valid.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
