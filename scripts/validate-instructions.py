#!/usr/bin/env python3
"""
Author-side sanity check for the AI-Instructions-Scaffold repo.

Catches drift before it reaches a consumer app. Run from the repo root:

    python scripts/validate-instructions.py
    py -3 scripts/validate-instructions.py        # Windows fallback

Checks:
  - Relative-link integrity in every Markdown file (no broken `../` paths).
  - Phase labels in instruction prose match the canonical set
    (Phase 1, Phase 2, Phase 3, Phase 4, Phase 5, 5a, 5b, 5c, 5d, 5e).
  - Each .claude/commands/*.md and .github/agents/*.md has the expected sections.
  - Each scaffold command/agent carries the maintenance-repo guard that stops
    accidental generation when `.instructions/` is missing.
  - The payload shape declared in install-to-project.py covers every top-level
    runtime directory present in this repo (catches "added skills/foo, forgot
    to wire it into the installer" mistakes).
  - Installer smoke checks cover every first-class harness entrypoint.
  - Section-anchor existence: when prose says ``[label](file.md) § Section Name``
    or ``file.md → Section Name`` (with the path in backticks), verify the named
    section exists as a heading in the target file. Catches refs left dangling
    after file splits.

Exit code 0 if all checks pass, 1 otherwise. Output groups failures by file.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

# Force UTF-8 stdout/stderr so non-ASCII characters in messages (e.g. → arrow,
# § section sign) don't crash on Windows consoles that default to cp1252.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

REPO_ROOT = Path(__file__).resolve().parent.parent

# Directories the runtime payload should ship. Must match install-to-project.py.
EXPECTED_RUNTIME_DIRS = {"ai", "patterns", "schemas", "skills", "support", "templates", "scripts"}

# Author-side directories that must NOT be in the runtime payload.
AUTHOR_ONLY_DIRS = {"tests", ".github/workflows", ".githooks", ".vscode", ".venv", ".tmp"}

# Markdown roots to walk. Skip vendored/temporary trees.
SCAN_ROOTS = ["ai", "patterns", "schemas", "skills", "support", "templates", ".claude", ".github"]
TOP_LEVEL_MD = ["README.md", "START-AI.md", "CLAUDE.md", "AGENTS.md"]

EXCLUDE_PARTS = {"__pycache__", ".git", ".venv", ".tmp", ".vscode", ".githooks", "tests", "bin", "obj", "node_modules"}

# Recognized phase tokens. Anything matching the pattern but not on this list is flagged.
CANONICAL_PHASES = {"Phase 1", "Phase 2", "Phase 3", "Phase 4", "Phase 5", "Phase 5a", "Phase 5b", "Phase 5c", "Phase 5d", "Phase 5e"}
PHASE_PATTERN = re.compile(r"\bPhase\s+(\d+[a-z]?)\b")

# Inline link pattern: [text](target). Skip absolute URLs and anchors-only.
LINK_PATTERN = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")

# Markdown headings (ATX style) for the section-anchor check.
HEADING_PATTERN = re.compile(r"^#{1,6}\s+(.+?)\s*$", re.MULTILINE)

# Prose pattern: `[label](file.md) § Section`, `[label](file.md) → *Section*`,
# or the same with a backtick-wrapped path instead of a link. Captures:
#   group "linkpath": path inside (...) — None for backtick form
#   group "tickpath": path inside `...` — None for link form
#   group "section":  raw section text (may include leading/trailing `*`)
SECTION_REF_PATTERN = re.compile(
    r"(?:\[[^\]]+\]\((?P<linkpath>[^)]+?\.md)(?:#[^)]*)?\)|`(?P<tickpath>[^`]+?\.md)`)"
    r"\s*(?:§|→)\s*"
    r"(?P<section>(?:\*[^*\n]{1,80}\*|[^\n.|,)(`*—–]{1,80}))"
)

# Connectors that end a section name when used like `... see X.md § Foo and bar baz`.
SECTION_TAIL_CONNECTOR = re.compile(
    r"\s+(and|or|see|for|in|of|on|via|when|to|with|before|after|then)\b.*$",
    re.IGNORECASE,
)

# Required section headings in harness command/agent files.
REQUIRED_COMMAND_HEADINGS = {
    ".claude/commands/scaffold.md": ["Instructions", "Rules"],
    ".claude/commands/vertical-slice.md": ["Instructions", "Pre-Flight", "Rules"],
    ".claude/commands/scaffold-adopt.md": ["Instructions", "Pre-Flight", "Rules"],
    ".github/agents/dotnet-scaffold.agent.md": ["Bootstrap", "Core Rules"],
    ".github/agents/vertical-slice.agent.md": ["Bootstrap", "Pre-Flight", "Constraints"],
    ".github/agents/scaffold-adopt.agent.md": ["Bootstrap", "Pre-Flight", "Constraints"],
}

MAINTENANCE_GUARD_PHRASES = ["Maintenance-repo note", "If `.instructions/` is missing"]

EXPECTED_SMOKE_CHECK_HARNESS_ENTRYPOINTS = {
    "AGENTS.md",
    "CLAUDE.md",
    ".github/copilot-instructions.md",
    ".claude/commands/scaffold.md",
    ".claude/commands/vertical-slice.md",
    ".claude/commands/scaffold-adopt.md",
    ".github/agents/dotnet-scaffold.agent.md",
    ".github/agents/vertical-slice.agent.md",
    ".github/agents/scaffold-adopt.agent.md",
}


class Findings:
    def __init__(self) -> None:
        self.errors: list[tuple[Path, str]] = []
        self.warnings: list[tuple[Path, str]] = []

    def err(self, path: Path, msg: str) -> None:
        self.errors.append((path, msg))

    def warn(self, path: Path, msg: str) -> None:
        self.warnings.append((path, msg))

    def report(self) -> int:
        if not self.errors and not self.warnings:
            print("[ok] all checks passed")
            return 0

        if self.errors:
            print(f"[fail] {len(self.errors)} error(s):")
            grouped: dict[Path, list[str]] = {}
            for path, msg in self.errors:
                grouped.setdefault(path, []).append(msg)
            for path in sorted(grouped, key=lambda p: str(p)):
                print(f"  {path.relative_to(REPO_ROOT).as_posix()}")
                for msg in grouped[path]:
                    print(f"    - {msg}")

        if self.warnings:
            print()
            print(f"[warn] {len(self.warnings)} warning(s):")
            grouped_w: dict[Path, list[str]] = {}
            for path, msg in self.warnings:
                grouped_w.setdefault(path, []).append(msg)
            for path in sorted(grouped_w, key=lambda p: str(p)):
                print(f"  {path.relative_to(REPO_ROOT).as_posix()}")
                for msg in grouped_w[path]:
                    print(f"    - {msg}")

        return 1 if self.errors else 0


def iter_markdown_files() -> list[Path]:
    files: list[Path] = []
    for top in TOP_LEVEL_MD:
        p = REPO_ROOT / top
        if p.exists():
            files.append(p)
    for root in SCAN_ROOTS:
        base = REPO_ROOT / root
        if not base.exists():
            continue
        for path in base.rglob("*.md"):
            if any(part in EXCLUDE_PARTS for part in path.relative_to(REPO_ROOT).parts):
                continue
            files.append(path)
    return files


def check_links(path: Path, findings: Findings) -> None:
    text = path.read_text(encoding="utf-8")
    # Adjacent-duplicate detection: same [label](target) appearing twice in the
    # same line (the drift pattern we want to catch — copy/paste with no edit).
    for line_no, line in enumerate(text.splitlines(), start=1):
        line_seen: dict[tuple[str, str], int] = {}
        for match in LINK_PATTERN.finditer(line):
            key = (match.group(1), match.group(2).strip())
            if key in line_seen:
                findings.warn(path, f"line {line_no}: link [{key[0]}]({key[1]}) appears twice on the same line")
            line_seen[key] = match.start()

    for match in LINK_PATTERN.finditer(text):
        label, target = match.group(1), match.group(2).strip()
        if target.startswith(("http://", "https://", "mailto:", "#")):
            continue
        # Strip anchor.
        link_path = target.split("#", 1)[0]
        if not link_path:
            continue
        # Resolve relative to the file's parent.
        candidate = (path.parent / link_path).resolve()
        if not candidate.exists():
            findings.err(path, f"broken link: [{label}]({target}) -> {candidate}")


def check_phase_labels(path: Path, findings: Findings) -> None:
    text = path.read_text(encoding="utf-8")
    for match in PHASE_PATTERN.finditer(text):
        token = "Phase " + match.group(1)
        if token not in CANONICAL_PHASES:
            line_no = text.count("\n", 0, match.start()) + 1
            findings.err(path, f"line {line_no}: unrecognized phase token '{token}' (canonical: 1-5, 5a-5e)")


def collect_headings(path: Path, cache: dict[Path, list[str]]) -> list[str]:
    if path in cache:
        return cache[path]
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        cache[path] = []
        return cache[path]
    cache[path] = [m.group(1).strip() for m in HEADING_PATTERN.finditer(text)]
    return cache[path]


def normalize_text(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip().lower()


def heading_matches_section(headings: list[str], target: str) -> bool:
    if not target:
        return False
    target_n = normalize_text(target)
    for h in headings:
        h_n = normalize_text(h)
        if h_n == target_n:
            return True
        # Prefix match for headings like "Menu Navigation: Always Land On Top Page"
        # vs reference "Menu Navigation".
        if h_n.startswith(target_n + " ") or h_n.startswith(target_n + ":") or h_n.startswith(target_n + " —") or h_n.startswith(target_n + " –"):
            return True
        # Substring fallback for short identifiers like "5a" -> "5a — Foundation (TDD)".
        if len(target_n) <= 6 and target_n in h_n:
            return True
    return False


def check_section_anchors(path: Path, findings: Findings, headings_cache: dict[Path, list[str]]) -> None:
    text = path.read_text(encoding="utf-8")
    for line_no, line in enumerate(text.splitlines(), start=1):
        for match in SECTION_REF_PATTERN.finditer(line):
            target_link = match.group("linkpath") or match.group("tickpath")
            section = match.group("section").strip().strip("*").strip()
            section = SECTION_TAIL_CONNECTOR.sub("", section)
            section = section.rstrip(".,;:")
            if not section or len(section) < 2:
                continue
            link_path = target_link.split("#", 1)[0]
            try:
                candidate = (path.parent / link_path).resolve()
            except (OSError, ValueError):
                continue
            if not candidate.exists() or not candidate.is_file() or candidate.suffix != ".md":
                # Broken file path — handled by check_links.
                continue
            headings = collect_headings(candidate, headings_cache)
            if not heading_matches_section(headings, section):
                rel = candidate.relative_to(REPO_ROOT).as_posix() if REPO_ROOT in candidate.parents or candidate == REPO_ROOT else candidate.name
                findings.err(
                    path,
                    f"line {line_no}: section anchor not found — {rel} has no heading matching '{section}'",
                )


def check_command_shape(findings: Findings) -> None:
    for rel, required_headings in REQUIRED_COMMAND_HEADINGS.items():
        path = REPO_ROOT / rel
        if not path.exists():
            findings.err(path, "expected command/agent file is missing")
            continue
        text = path.read_text(encoding="utf-8")
        for heading in required_headings:
            # Match `## Heading` or `## Heading ...` exactly at line start.
            pattern = re.compile(rf"^##\s+{re.escape(heading)}\b", re.MULTILINE)
            if not pattern.search(text):
                findings.err(path, f"missing required heading '## {heading}'")


def check_maintenance_guards(findings: Findings) -> None:
    for rel in REQUIRED_COMMAND_HEADINGS:
        path = REPO_ROOT / rel
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8")
        for phrase in MAINTENANCE_GUARD_PHRASES:
            if phrase not in text:
                findings.err(path, f"missing maintenance-repo guard phrase: {phrase}")


def check_payload_shape(findings: Findings) -> None:
    """Compare what install-to-project.py copies vs what's actually present at the repo root."""
    installer = REPO_ROOT / "scripts" / "install-to-project.py"
    if not installer.exists():
        findings.err(installer, "install-to-project.py is missing — payload shape unverifiable")
        return
    text = installer.read_text(encoding="utf-8")
    # Extract the INSTRUCTIONS_DIRS list literal.
    match = re.search(r"INSTRUCTIONS_DIRS\s*=\s*\[(.*?)\]", text, re.DOTALL)
    if not match:
        findings.err(installer, "could not parse INSTRUCTIONS_DIRS list")
        return
    declared = set(re.findall(r'"([^"]+)"', match.group(1)))

    if declared != EXPECTED_RUNTIME_DIRS:
        missing = EXPECTED_RUNTIME_DIRS - declared
        extra = declared - EXPECTED_RUNTIME_DIRS
        if missing:
            findings.err(installer, f"INSTRUCTIONS_DIRS missing dirs the validator expects: {sorted(missing)}")
        if extra:
            findings.err(installer, f"INSTRUCTIONS_DIRS contains unexpected dirs: {sorted(extra)} (update validator if intentional)")

    # Each declared dir must exist in the repo with at least one file.
    for d in declared:
        target = REPO_ROOT / d
        if not target.exists() or not target.is_dir():
            findings.err(installer, f"declared payload dir '{d}/' missing or not a directory")
        elif not any(target.iterdir()):
            findings.warn(installer, f"declared payload dir '{d}/' is empty")

    smoke_match = re.search(r"SMOKE_CHECK_HARNESS_ENTRYPOINTS\s*=\s*\[(.*?)\]", text, re.DOTALL)
    if not smoke_match:
        findings.err(installer, "could not parse SMOKE_CHECK_HARNESS_ENTRYPOINTS list")
        return
    smoke_declared = set(re.findall(r'"([^"]+)"', smoke_match.group(1)))
    if smoke_declared != EXPECTED_SMOKE_CHECK_HARNESS_ENTRYPOINTS:
        missing = EXPECTED_SMOKE_CHECK_HARNESS_ENTRYPOINTS - smoke_declared
        extra = smoke_declared - EXPECTED_SMOKE_CHECK_HARNESS_ENTRYPOINTS
        if missing:
            findings.err(installer, f"SMOKE_CHECK_HARNESS_ENTRYPOINTS missing first-class entrypoints: {sorted(missing)}")
        if extra:
            findings.err(installer, f"SMOKE_CHECK_HARNESS_ENTRYPOINTS contains unexpected entries: {sorted(extra)}")


def main() -> int:
    findings = Findings()

    md_files = iter_markdown_files()
    headings_cache: dict[Path, list[str]] = {}
    for path in md_files:
        check_links(path, findings)
        check_phase_labels(path, findings)
        check_section_anchors(path, findings, headings_cache)

    check_command_shape(findings)
    check_maintenance_guards(findings)
    check_payload_shape(findings)

    print(f"validated {len(md_files)} markdown file(s) under {REPO_ROOT}")
    print()
    return findings.report()


if __name__ == "__main__":
    sys.exit(main())
