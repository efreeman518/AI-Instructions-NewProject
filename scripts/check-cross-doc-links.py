#!/usr/bin/env python3
"""Verify markdown relative links across the instruction set resolve to real files.

Walks markdown files, extracts `[text](relative/path.md)` and `[text](relative/path.md#anchor)`
links, and fails on any path that doesn't exist on disk. Skips http(s) links and
mailto/anchor-only links.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

LINK_RE = re.compile(r"\[(?P<text>[^\]]+)\]\((?P<href>[^)\s]+)(?:\s+\"[^\"]*\")?\)")
SKIP_PREFIXES = ("http://", "https://", "mailto:", "#", "tel:", "data:")

DEFAULT_INCLUDE_DIRS = ["ai", "support", "templates", "skills", "patterns", "schemas", "scripts"]
DEFAULT_INCLUDE_EXTS = {".md"}


def find_files(root: Path) -> list[Path]:
    files: list[Path] = []
    for d in DEFAULT_INCLUDE_DIRS:
        base = root / d
        if not base.exists():
            continue
        for p in base.rglob("*"):
            if p.is_file() and p.suffix in DEFAULT_INCLUDE_EXTS:
                files.append(p)
    for p in root.glob("*.md"):
        if p.is_file():
            files.append(p)
    return files


def check_links(md: Path, root: Path) -> list[tuple[int, str, str]]:
    """Return list of (lineno, href, reason) for broken links."""
    broken: list[tuple[int, str, str]] = []
    try:
        text = md.read_text(encoding="utf-8")
    except (UnicodeDecodeError, OSError):
        return broken

    in_code = False
    for lineno, line in enumerate(text.splitlines(), start=1):
        # Skip fenced code blocks
        if line.strip().startswith("```"):
            in_code = not in_code
            continue
        if in_code:
            continue
        for m in LINK_RE.finditer(line):
            href = m.group("href")
            if href.startswith(SKIP_PREFIXES):
                continue
            # Strip anchor and query
            path_part = href.split("#", 1)[0].split("?", 1)[0]
            if not path_part:
                continue
            # Drive-letter Windows absolute paths: skip (out-of-scope cross-repo links).
            if re.match(r"^[a-zA-Z]:[\\/]", path_part):
                continue
            target = (md.parent / path_part).resolve()
            if not target.exists():
                broken.append((lineno, href, f"target not found: {target}"))
    return broken


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
    )
    args = parser.parse_args()

    root: Path = args.root.resolve()
    files = find_files(root)

    total = 0
    for md in files:
        broken = check_links(md, root)
        if broken:
            rel = md.relative_to(root).as_posix()
            for lineno, href, reason in broken:
                print(f"{rel}:{lineno}: broken link [{href}] - {reason}")
                total += 1

    if total:
        print(f"\n{total} broken link(s) found.", file=sys.stderr)
        return 1

    print("OK: all relative markdown links resolve.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
