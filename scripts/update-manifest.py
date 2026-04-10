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
        relative_path.startswith("sample-app/")
        or re.search(r"(^|/)(\.git|bin|obj|\.venv|venv|env)/", relative_path) is not None
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

    # Scan repo .md files (exclude sample-app, build output, VCS, and local venvs)
    md_files = [
        p for p in root.rglob("*.md")
        if not should_ignore_markdown_path(p.relative_to(root).as_posix())
    ]
    md_file_set = {p.relative_to(root).as_posix() for p in md_files}

    # Drop stale or ignored markdown entries before recalculating token counts.
    manifest["files"] = [
        entry for entry in manifest["files"]
        if not (
            str(entry["path"]).endswith(".md")
            and (
                str(entry["path"]) not in md_file_set
                or should_ignore_markdown_path(str(entry["path"]))
            )
        )
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
        # Normalize line endings to LF so token counts are consistent across Windows/Linux
        content = content.replace("\r\n", "\n")
        chars = len(content)
        tokens = math.ceil(chars / 4)

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

    # Also update _manifest.json's own token estimate
    manifest_relative = "_manifest.json"
    if manifest_relative in file_entries:
        manifest_content = manifest_path.read_text(encoding="utf-8")
        manifest_content = manifest_content.replace("\r\n", "\n")
        manifest_tokens = math.ceil(len(manifest_content) / 4)
        entry = file_entries[manifest_relative]
        if entry["estimatedTokens"] != manifest_tokens:
            entry["estimatedTokens"] = manifest_tokens
            updated += 1

    # Compute total
    total = 0
    for entry in manifest["files"]:
        entry["estimatedTokens"] = int(entry["estimatedTokens"])
        total += entry["estimatedTokens"]
    manifest["totalEstimatedTokens"] = total

    # Serialize and write back
    json_str = json.dumps(manifest, indent=4, ensure_ascii=False)
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json_str)

    print(f"Updated {updated} files, added {added} new entries, total tokens: {total}")


if __name__ == "__main__":
    main()
