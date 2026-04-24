#!/usr/bin/env python3
"""
Installs the runtime payload of this instruction repo into a consumer app.

Copies:
    <repo>/<runtime files>    -> <target>/.instructions/
    <repo>/AGENTS.md          -> <target>/AGENTS.md              (unless --instructions-only)
    <repo>/.claude/commands/  -> <target>/.claude/commands/      (unless --instructions-only)
    <repo>/.github/agents/    -> <target>/.github/agents/        (unless --instructions-only)

Excludes author-side files: scripts/__pycache__, tests/, .git/, .github/workflows/,
.github/copilot-instructions.md, .githooks/, .vscode/, .venv/, .tmp/, .gitignore.

Usage:
    python scripts/install-to-project.py --target <app-repo-root>
    python scripts/install-to-project.py --target <app-repo-root> --update
    python scripts/install-to-project.py --target <app-repo-root> --dry-run
    python scripts/install-to-project.py --target <app-repo-root> --instructions-only
"""

import argparse
import shutil
import sys
from pathlib import Path


# Runtime payload copied into <target>/.instructions/
INSTRUCTIONS_FILES = [
    "README.md",
    "CLAUDE.md",
    "START-AI.md",
    "phase-load-packs.json",
    "_manifest.json",
    "payload-manifest.json",
]

INSTRUCTIONS_DIRS = [
    "ai",
    "patterns",
    "schemas",
    "skills",
    "support",
    "templates",
    "scripts",
]

# Paths excluded from any directory copy (matched against path parts).
EXCLUDE_PARTS = {
    "__pycache__",
    ".git",
    ".venv",
    ".tmp",
    ".vscode",
    ".githooks",
    "tests",
    "bin",
    "obj",
}

# Agent/command placements that land at the app repo root, not under .instructions/.
AGENT_COPIES = [
    ("AGENTS.md", "AGENTS.md", "file"),
    (".claude/commands", ".claude/commands", "dir"),
    (".github/agents", ".github/agents", "dir"),
]


class Planner:
    def __init__(self, dry_run: bool, update: bool):
        self.dry_run = dry_run
        self.update = update
        self.copied = 0
        self.skipped = 0
        self.preserved = 0

    def _should_skip(self, rel_path: Path) -> bool:
        return any(part in EXCLUDE_PARTS for part in rel_path.parts)

    def copy_file(self, src: Path, dst: Path, label: str) -> None:
        if dst.exists() and self.update:
            src_mtime = src.stat().st_mtime
            dst_mtime = dst.stat().st_mtime
            if dst_mtime > src_mtime:
                print(f"  [preserve] {label} (target is newer)")
                self.preserved += 1
                return
        action = "[dry-run]" if self.dry_run else "[copy]"
        print(f"  {action} {label}")
        if not self.dry_run:
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
        self.copied += 1

    def copy_tree(self, src: Path, dst: Path, label_prefix: str) -> None:
        if not src.exists():
            print(f"  [skip]  {label_prefix} (missing in source)")
            self.skipped += 1
            return
        for path in src.rglob("*"):
            if path.is_dir():
                continue
            rel = path.relative_to(src)
            if self._should_skip(rel):
                continue
            self.copy_file(path, dst / rel, f"{label_prefix}/{rel.as_posix()}")

    def summary(self) -> None:
        print()
        print(f"copied:    {self.copied}")
        print(f"preserved: {self.preserved} (target newer, --update)")
        print(f"skipped:   {self.skipped}")
        if self.dry_run:
            print("(dry-run — no files written)")


def preserve_handoff(target_instructions: Path, dry_run: bool) -> Path | None:
    handoff = target_instructions.parent / "HANDOFF.md"
    if handoff.exists():
        print(f"[note] HANDOFF.md exists at {handoff} — left untouched")
        return handoff
    return None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Install AI-Instructions runtime payload into a consumer app.",
    )
    parser.add_argument(
        "--target", required=True,
        help="Path to the consumer app repo root (parent of .instructions/).",
    )
    parser.add_argument(
        "--update", action="store_true",
        help="Skip files where the target copy is newer than the source.",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Print planned copies without writing files.",
    )
    parser.add_argument(
        "--instructions-only", action="store_true",
        help="Install only <target>/.instructions/, skip agent/command placement.",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parent.parent
    target_root = Path(args.target).resolve()

    if not target_root.exists():
        print(f"error: target does not exist: {target_root}", file=sys.stderr)
        return 1
    if not target_root.is_dir():
        print(f"error: target is not a directory: {target_root}", file=sys.stderr)
        return 1
    if target_root == repo_root:
        print("error: refusing to install into the instruction repo itself", file=sys.stderr)
        return 1

    target_instructions = target_root / ".instructions"
    print(f"source: {repo_root}")
    print(f"target: {target_root}")
    print()

    preserve_handoff(target_instructions, args.dry_run)

    planner = Planner(dry_run=args.dry_run, update=args.update)

    print("== .instructions/ payload ==")
    for rel in INSTRUCTIONS_FILES:
        src = repo_root / rel
        if not src.exists():
            print(f"  [skip]  {rel} (missing in source)")
            planner.skipped += 1
            continue
        planner.copy_file(src, target_instructions / rel, f".instructions/{rel}")

    for rel in INSTRUCTIONS_DIRS:
        planner.copy_tree(
            repo_root / rel,
            target_instructions / rel,
            f".instructions/{rel}",
        )

    if not args.instructions_only:
        print()
        print("== agent/command placement (app repo root) ==")
        for src_rel, dst_rel, kind in AGENT_COPIES:
            src = repo_root / src_rel
            dst = target_root / dst_rel
            if not src.exists():
                print(f"  [skip]  {dst_rel} (missing in source)")
                planner.skipped += 1
                continue
            if kind == "file":
                planner.copy_file(src, dst, dst_rel)
            else:
                planner.copy_tree(src, dst, dst_rel)

    planner.summary()
    return 0


if __name__ == "__main__":
    sys.exit(main())
