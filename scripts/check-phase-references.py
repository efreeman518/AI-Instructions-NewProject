#!/usr/bin/env python3
"""Fail if any active doc still references old Phase 5f or 5g taxonomy.

Phase 5 was consolidated to 5a-5e. Old references must be removed from active
docs to avoid drift between canonical phase model and satellite files.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

PHASE_PATTERN = re.compile(r"\b5[fg]\b")

DEFAULT_INCLUDE_DIRS = ["ai", "support", "templates", "skills", "patterns"]
DEFAULT_INCLUDE_EXTS = {".md", ".yaml", ".yml", ".json"}

# Files allowed to mention 5f/5g for legitimate reasons (e.g., this script itself).
ALLOWLIST = {
    "scripts/check-phase-references.py",
}


def find_files(root: Path, include_dirs: list[str], exts: set[str]) -> list[Path]:
    files: list[Path] = []
    for d in include_dirs:
        base = root / d
        if not base.exists():
            continue
        for p in base.rglob("*"):
            if p.is_file() and p.suffix in exts:
                files.append(p)
    # Also check root-level markdown
    for p in root.glob("*.md"):
        if p.is_file():
            files.append(p)
    return files


def is_allowlisted(path: Path, root: Path) -> bool:
    rel = path.relative_to(root).as_posix()
    return rel in ALLOWLIST


def scan(path: Path) -> list[tuple[int, str]]:
    hits: list[tuple[int, str]] = []
    try:
        text = path.read_text(encoding="utf-8")
    except (UnicodeDecodeError, OSError):
        return hits
    for lineno, line in enumerate(text.splitlines(), start=1):
        if PHASE_PATTERN.search(line):
            hits.append((lineno, line.strip()))
    return hits


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
        help="Repo root (default: parent of scripts/).",
    )
    args = parser.parse_args()

    root: Path = args.root.resolve()
    files = find_files(root, DEFAULT_INCLUDE_DIRS, DEFAULT_INCLUDE_EXTS)

    total_hits = 0
    for f in files:
        if is_allowlisted(f, root):
            continue
        hits = scan(f)
        if hits:
            rel = f.relative_to(root).as_posix()
            for lineno, line in hits:
                print(f"{rel}:{lineno}: stale phase reference -> {line}")
                total_hits += 1

    if total_hits:
        print(
            f"\n{total_hits} stale 5f/5g reference(s) found. "
            "Phase 5 taxonomy is 5a-5e; remove these references.",
            file=sys.stderr,
        )
        return 1

    print("OK: no stale 5f/5g references found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
