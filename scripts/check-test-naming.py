#!/usr/bin/env python3
"""Fail if any doc references legacy test-project names that conflict with the canonical set.

Canonical test projects:
  Test.Unit, Test.Integration, Test.Endpoints, Test.E2E, Test.PlaywrightUI,
  Test.Architecture, Test.Load, Test.Benchmarks, Test.Support

This script flags:
  - 'Test.E2E.Playwright' (reference-app legacy name; should be Test.PlaywrightUI)
  - 'Test.Integration/Endpoints/' (endpoint tests now live in Test.Endpoints)
  - 'Test.Integration.TestDB' (the test-DB name moved to Test.Endpoints.TestDB)

Add additional checks here as the canonical set evolves.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

PATTERNS: list[tuple[re.Pattern[str], str]] = [
    (re.compile(r"Test\.E2E\.Playwright\b"), "use Test.PlaywrightUI instead"),
    (re.compile(r"Test\.Integration/Endpoints\b"), "endpoint tests live in Test.Endpoints"),
    (re.compile(r"Test\.Integration\.TestDB\b"), "test-DB name should be Test.Endpoints.TestDB"),
]

DEFAULT_INCLUDE_DIRS = ["ai", "support", "templates", "skills", "patterns"]
DEFAULT_INCLUDE_EXTS = {".md", ".yaml", ".yml", ".json"}

# Files allowed to mention these patterns (e.g., this script).
ALLOWLIST = {
    "scripts/check-test-naming.py",
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
    for p in root.glob("*.md"):
        if p.is_file():
            files.append(p)
    return files


def is_allowlisted(path: Path, root: Path) -> bool:
    return path.relative_to(root).as_posix() in ALLOWLIST


def scan(path: Path) -> list[tuple[int, str, str]]:
    hits: list[tuple[int, str, str]] = []
    try:
        text = path.read_text(encoding="utf-8")
    except (UnicodeDecodeError, OSError):
        return hits
    for lineno, line in enumerate(text.splitlines(), start=1):
        for pattern, message in PATTERNS:
            if pattern.search(line):
                hits.append((lineno, message, line.strip()))
    return hits


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
    )
    args = parser.parse_args()

    root: Path = args.root.resolve()
    files = find_files(root, DEFAULT_INCLUDE_DIRS, DEFAULT_INCLUDE_EXTS)

    total = 0
    for f in files:
        if is_allowlisted(f, root):
            continue
        hits = scan(f)
        if hits:
            rel = f.relative_to(root).as_posix()
            for lineno, message, line in hits:
                print(f"{rel}:{lineno}: {message} -> {line}")
                total += 1

    if total:
        print(f"\n{total} legacy test-naming reference(s) found.", file=sys.stderr)
        return 1

    print("OK: test-naming canonical set is consistent.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
