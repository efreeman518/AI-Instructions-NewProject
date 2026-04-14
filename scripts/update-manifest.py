#!/usr/bin/env python3
"""
Updates _manifest.json token estimates by scanning all .md files.

1. Scans all .md files.
2. Computes ceil(chars / 4) per file.
3. Reads _manifest.json, updates estimatedTokens for matching paths.
4. Adds new entries for untracked .md files with "phase": "unknown".
5. Updates totalEstimatedTokens.
6. Writes back with consistent formatting.
7. Reports summary.
"""

import argparse
import json
import math
import re
import sys
from pathlib import Path


def should_ignore_markdown_path(relative_path):
    return (
        re.search(r"(^|/)(\.git|\.github|\.claude|bin|obj|\.venv|venv|env)/", relative_path) is not None
    )


def estimate_tokens(text):
    normalized = text.replace("\r\n", "\n")
    return math.ceil(len(normalized) / 4)


def serialize_manifest(manifest):
    return json.dumps(manifest, indent=4, ensure_ascii=False)


def is_manifest_scoped_path(relative_path):
    return relative_path == "_manifest.json" or (
        relative_path.endswith(".md")
        and not should_ignore_markdown_path(relative_path)
    )


def main():
    parser = argparse.ArgumentParser(description="Update _manifest.json token estimates")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent

    manifest_path = root / "_manifest.json"
    if not manifest_path.exists():
        print(f"Manifest not found at {manifest_path}", file=sys.stderr)
        sys.exit(1)

    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    # Scan repo .md files (exclude build output, VCS, and local venvs)
    md_files = [
        p for p in root.rglob("*.md")
        if not should_ignore_markdown_path(p.relative_to(root).as_posix())
    ]
    md_file_set = {p.relative_to(root).as_posix() for p in md_files}

    scoped_file_set = md_file_set | {"_manifest.json"}

    # Drop entries that are no longer inside the declared manifest scope.
    manifest["files"] = [
        entry for entry in manifest["files"]
        if str(entry["path"]) in scoped_file_set and is_manifest_scoped_path(str(entry["path"]))
    ]

    # Build lookup of existing entries by path
    file_entries = {}
    for entry in manifest["files"]:
        file_entries[entry["path"]] = entry

    updated = 0
    added = 0

    for md_file in md_files:
        relative_path = md_file.relative_to(root).as_posix()
        content = md_file.read_text(encoding="utf-8")
        tokens = estimate_tokens(content)

        if relative_path in file_entries:
            entry = file_entries[relative_path]
            if int(entry["estimatedTokens"]) != tokens:
                entry["estimatedTokens"] = tokens
                updated += 1
        else:
            new_entry = {
                "path": relative_path,
                "phase": "unknown",
                "estimatedTokens": tokens,
            }
            manifest["files"].append(new_entry)
            file_entries[relative_path] = new_entry
            added += 1

    manifest_relative = "_manifest.json"
    for entry in manifest["files"]:
        entry["estimatedTokens"] = int(entry["estimatedTokens"])

    total = 0
    json_str = ""
    manifest_entry = file_entries.get(manifest_relative)
    previous_manifest_tokens = None

    while True:
        total = sum(int(entry["estimatedTokens"]) for entry in manifest["files"])
        manifest["totalEstimatedTokens"] = total
        json_str = serialize_manifest(manifest)

        if manifest_entry is None:
            break

        manifest_tokens = estimate_tokens(json_str)
        if int(manifest_entry["estimatedTokens"]) == manifest_tokens:
            break

        if previous_manifest_tokens != manifest_tokens:
            updated += 1
        previous_manifest_tokens = manifest_tokens
        manifest_entry["estimatedTokens"] = manifest_tokens

    with open(manifest_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json_str)

    print(f"Updated {updated} files, added {added} new entries, total tokens: {total}")


if __name__ == "__main__":
    main()
